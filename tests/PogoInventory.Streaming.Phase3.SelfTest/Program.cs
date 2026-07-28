using System.Runtime.CompilerServices;
using PogoInventory.Streaming;
using PogoInventory.Streaming.Gates;
using PogoInventory.Streaming.Scrcpy;

namespace PogoInventory.Streaming.Phase3.SelfTest;

internal static class Program
{
    private static readonly DateTimeOffset BaseUtc = new(2026, 7, 28, 8, 0, 0, TimeSpan.Zero);

    public static async Task<int> Main()
    {
        var tests = new (string Name, Func<Task> Run)[]
        {
            ("Built-in profiles separate volatile animation from required stability", BuiltInProfilesSeparateVolatileAnimationAsync),
            ("Pixel observer tolerates animated model and background", PixelObserverToleratesAnimatedModelAsync),
            ("StableRegionGate passes with volatile motion", StableGatePassesWithVolatileMotionAsync),
            ("Required-region motion blocks stability", RequiredRegionMotionBlocksAsync),
            ("TransitionDetectedGate rejects isolated noise", TransitionDetectionRequiresSustainedChangeAsync),
            ("TransitionCompletedGate passes meaningful regional progression", TransitionCompletedPassesAsync),
            ("Animated background cannot fake progression", AnimatedBackgroundCannotFakeProgressionAsync),
            ("FrameSetSelector chooses regional frames and releases leases", FrameSetSelectorWorksAsync),
            ("TemporalGateSession is bounded and rejects out-of-order frames", SessionIsBoundedAsync),
            ("Gate composition is fail-closed", CompositionIsFailClosedAsync),
            ("Engine reports timeout, cancellation and fault precisely", EngineTerminationIsPreciseAsync),
            ("Freeze detection is temporal", FreezeDetectionIsTemporalAsync),
            ("ROI view respects stride and lifetime", RoiViewRespectsStrideAsync),
            ("Public surface remains read-only", PublicSurfaceIsReadOnlyAsync),
            ("Same sequence gives deterministic result", GateEvaluationIsDeterministicAsync)
        };

        var passed = 0;
        foreach (var test in tests)
        {
            try
            {
                await test.Run().ConfigureAwait(false);
                passed++;
                Console.WriteLine($"PASS: {test.Name}");
            }
            catch (Exception error)
            {
                Console.Error.WriteLine($"FAIL: {test.Name}");
                Console.Error.WriteLine(error);
                return 1;
            }
        }

        Console.WriteLine($"Phase 3 self-test passed: {passed}/{tests.Length}");
        Console.WriteLine("Input commands sent: 0");
        return 0;
    }

    private static Task BuiltInProfilesSeparateVolatileAnimationAsync()
    {
        var profile = BuiltInGateProfiles.StableHeaderAndPanel;
        profile.Validate();
        Assert(profile.Stable.RequiredRegions.SequenceEqual(new[] { "Header", "Panel", "BottomControl" }), "Required regions changed unexpectedly.");
        Assert(profile.Regions.Single(x => x.Name == "Model").StabilityRole == RegionStabilityRole.Volatile, "Model must be volatile.");
        Assert(profile.Regions.Single(x => x.Name == "AnimatedBackground").StabilityRole == RegionStabilityRole.Volatile, "Animated background must be volatile.");
        Assert(!profile.Stable.RequiredRegions.Contains("Model", StringComparer.Ordinal), "Model must not be a required stability region.");
        Assert(!profile.Stable.RequiredRegions.Contains("AnimatedBackground", StringComparer.Ordinal), "Animated background must not be a required stability region.");
        Assert(!profile.Regions.Single(x => x.Name == "AnimatedBackground").ObserveTransition, "Background animation must not create transition evidence.");
        return Task.CompletedTask;
    }

    private static async Task PixelObserverToleratesAnimatedModelAsync()
    {
        var profile = BuiltInGateProfiles.StableHeaderAndPanel;
        await using var observer = new MultiRegionTemporalObserver(profile.Regions, profile.Observer);
        TemporalFrameObservation? last = null;
        for (var index = 0; index < 5; index++)
        {
            using var retained = new RetainedFrame(CreatePixelLease(index + 1, index + 1, index * 100, modelVariant: index % 2, requiredVariant: 0));
            last = await observer.AnalyzeAsync(retained).ConfigureAwait(false);
        }

        Assert(last is not null, "No observation was produced.");
        var finalObservation = last!;
        Assert(finalObservation.IsLikelyStable, "Required regions should be stable despite animated model/background.");
        Assert(!finalObservation.Regions["Model"].IsLikelyStable, "Model motion should remain visible diagnostically.");
        Assert(!finalObservation.Regions["AnimatedBackground"].IsLikelyStable, "Background motion should remain visible diagnostically.");
        Assert(finalObservation.Regions["Header"].IsLikelyStable, "Header should be stable.");
        Assert(finalObservation.Regions["Panel"].IsLikelyStable, "Panel should be stable.");
    }

    private static async Task StableGatePassesWithVolatileMotionAsync()
    {
        var profile = BuiltInGateProfiles.StableHeaderAndPanel;
        var observations = Enumerable.Range(1, 5)
            .Select(index => Observation(index, index * 100, requiredStable: true, volatileMoving: true, transition: false, requiredFingerprint: 0x1111111111111111UL, fullFingerprint: (ulong)index))
            .ToArray();
        var result = await RunGateAsync(new StableRegionGate("stable", profile.Stable, profile.Regions), observations).ConfigureAwait(false);
        Assert(result.GateState == TemporalGateState.Passed, $"Expected PASS, got {result.GateState}/{result.ReasonCode}.");
        Assert(result.Diagnostics.TryGetValue("VolatileRegionsMoving", out var moving) && moving is true, "Volatile motion was not reported.");
        Assert(result.Diagnostics.TryGetValue("RequiredRegionsStable", out var stable) && stable is true, "Required stability was not documented.");
    }

    private static async Task RequiredRegionMotionBlocksAsync()
    {
        var profile = BuiltInGateProfiles.StableHeaderAndPanel;
        var observations = Enumerable.Range(1, 5)
            .Select(index => Observation(index, index * 100, requiredStable: false, volatileMoving: false, transition: false, requiredFingerprint: 1, fullFingerprint: 1))
            .ToArray();
        var result = await RunGateAsync(new StableRegionGate("stable", profile.Stable, profile.Regions), observations).ConfigureAwait(false);
        Assert(result.GateState == TemporalGateState.Rejected, "Stream end without stable evidence must reject.");
        Assert(result.ReasonCode == GateReasonCode.InsufficientEvidence, "Stream-end rejection must be explicit.");
    }

    private static async Task TransitionDetectionRequiresSustainedChangeAsync()
    {
        var options = BuiltInGateProfiles.GenericScreenTransition.Transition;
        var gate = new TransitionDetectedGate("transition", options);
        var sequence = new[]
        {
            Observation(1, 0, true, false, false, 1, 1),
            Observation(2, 100, false, false, true, 2, 2),
            Observation(3, 200, true, false, false, 1, 1),
            Observation(4, 300, false, false, true, 2, 2),
            Observation(5, 400, false, false, true, 3, 3)
        };
        var result = await RunGateAsync(gate, sequence).ConfigureAwait(false);
        Assert(result.GateState == TemporalGateState.Passed, "Two sustained change frames should pass.");
        Assert(result.SelectedEvidenceFrameIds.Select(x => x.Value).SequenceEqual(new long[] { 4, 5 }), "Isolated noise frame was incorrectly retained as transition evidence.");
    }

    private static async Task TransitionCompletedPassesAsync()
    {
        var profile = BuiltInGateProfiles.GenericScreenTransition;
        var result = await RunGateAsync(
            GateFactory.Create(profile),
            MeaningfulTransitionSequence()).ConfigureAwait(false);
        Assert(result.GateState == TemporalGateState.Passed, $"Expected transition PASS, got {result.GateState}/{result.ReasonCode}.");
        Assert(result.ReasonCode == GateReasonCode.Passed, "Transition pass reason is wrong.");
        Assert(result.Diagnostics.TryGetValue("ChangeMagnitude", out var magnitude) && Convert.ToDouble(magnitude) >= profile.Transition.MinimumMeaningfulChange, "Meaningful change was not documented.");
    }

    private static async Task AnimatedBackgroundCannotFakeProgressionAsync()
    {
        var profile = BuiltInGateProfiles.GenericScreenTransition;
        var sequence = new List<TemporalFrameObservation>
        {
            Observation(1, 0, true, true, false, 0x1111, 1),
            Observation(2, 100, true, true, false, 0x1111, 2),
            Observation(3, 200, true, true, false, 0x1111, 3),
            Observation(4, 300, false, true, true, 0x1111, 4),
            Observation(5, 400, false, true, true, 0x1111, 5),
            Observation(6, 500, true, true, false, 0x1111, 100),
            Observation(7, 600, true, true, false, 0x1111, 101),
            Observation(8, 700, true, true, false, 0x1111, 102)
        };
        var result = await RunGateAsync(GateFactory.Create(profile), sequence, GateTermination.Timeout).ConfigureAwait(false);
        Assert(result.GateState == TemporalGateState.TimedOut, "Background-only change must not pass progression.");
        Assert(result.ReasonCode == GateReasonCode.NoMeaningfulVisualProgression, $"Expected NoMeaningfulVisualProgression, got {result.ReasonCode}.");
    }

    private static async Task FrameSetSelectorWorksAsync()
    {
        var baseline = RetainedFrame.ActiveReferences;
        var profile = BuiltInGateProfiles.GenericScreenTransition;
        await using var session = new TemporalGateSession("selector", TimeSpan.FromSeconds(10), 16, 32, BaseUtc);
        var observations = MeaningfulTransitionSequence().ToArray();
        observations[0] = WithSharpness(observations[0], header: 0.30, panel: 0.40, model: 0.70);
        observations[1] = WithSharpness(observations[1], header: 0.95, panel: 0.45, model: 0.75);
        observations[2] = WithSharpness(observations[2], header: 0.50, panel: 0.96, model: 0.80);
        for (var index = 0; index < observations.Length; index++)
        {
            var retained = new RetainedFrame(CreateSolidLease(observations[index].FrameId.Value, observations[index].UtcTimestamp, (byte)(20 + index)));
            Assert(session.TryAdd(observations[index], retained, out _), "Could not add selector frame.");
        }

        var selector = new FrameSetSelector();
        using var selected = await selector.SelectAsync(
            session,
            new FrameSetRequest
            {
                Roles = new[]
                {
                    FrameRole.BestHeaderFrame,
                    FrameRole.BestPanelFrame,
                    FrameRole.BestOverallStableFrame,
                    FrameRole.PreTransitionFrame,
                    FrameRole.TransitionFrame,
                    FrameRole.PostTransitionFrame,
                    FrameRole.ConfirmationFrame
                },
                StableOptions = profile.Stable,
                TransitionOptions = profile.Transition,
                Diversity = profile.Diversity with
                {
                    MinimumFrameIdDistance = 1,
                    MinimumTimeDistance = TimeSpan.FromMilliseconds(80),
                    MaximumVisualSimilarity = 1.0
                }
            }).ConfigureAwait(false);

        Assert(selected.Frames[FrameRole.BestHeaderFrame].FrameId.Value == 2, "Best header frame selection failed.");
        Assert(selected.Frames[FrameRole.BestPanelFrame].FrameId.Value == 3, "Best panel frame selection failed.");
        Assert(selected.Frames.ContainsKey(FrameRole.TransitionFrame), "Transition frame was not selected.");
        Assert(selected.Frames.ContainsKey(FrameRole.PostTransitionFrame), "Post-transition frame was not selected.");
        selected.Dispose();
        await session.DisposeAsync().ConfigureAwait(false);
        Assert(RetainedFrame.ActiveReferences == baseline, "Frame selector leaked leases.");
    }

    private static async Task SessionIsBoundedAsync()
    {
        var firstRoot = new TestFrameLease(Metadata(1, 0), new byte[16]);
        await using var session = new TemporalGateSession("bounded", TimeSpan.FromSeconds(2), 2, 4, BaseUtc);
        Assert(session.TryAdd(Observation(1, 0, true, false, false, 1, 1), new RetainedFrame(firstRoot), out _), "Frame 1 rejected.");
        Assert(session.TryAdd(Observation(2, 100, true, false, false, 1, 1), new RetainedFrame(new TestFrameLease(Metadata(2, 100), new byte[16])), out _), "Frame 2 rejected.");
        Assert(session.TryAdd(Observation(3, 200, true, false, false, 1, 1), new RetainedFrame(new TestFrameLease(Metadata(3, 200), new byte[16])), out _), "Frame 3 rejected.");
        Assert(session.HistoryEvictions == 1, "History did not evict the oldest frame.");
        Assert(firstRoot.IsDisposed, "Evicted frame lease was not released.");
        var outOfOrder = new TestFrameLease(Metadata(2, 250), new byte[16]);
        Assert(!session.TryAdd(Observation(2, 250, true, false, false, 1, 1), new RetainedFrame(outOfOrder), out var reason), "Out-of-order frame was accepted.");
        Assert(reason == GateReasonCode.OutOfOrderFrame, "Wrong out-of-order reason.");
        Assert(outOfOrder.IsDisposed, "Rejected frame was not released.");

        var result = TemporalGateResult.Terminal("bounded", session, TemporalGateState.Rejected, GateReasonCode.InsufficientEvidence, 0, BaseUtc.AddMilliseconds(300));
        Assert(session.TryComplete(result), "Session did not complete.");
        Assert(!session.TryComplete(result), "Session completed twice.");
        var afterCompletion = new TestFrameLease(Metadata(4, 400), new byte[16]);
        Assert(!session.TryAdd(Observation(4, 400, true, false, false, 1, 1), new RetainedFrame(afterCompletion), out _), "Completed session accepted a frame.");
        Assert(afterCompletion.IsDisposed, "Frame added after completion was not released.");
    }

    private static async Task CompositionIsFailClosedAsync()
    {
        var observation = Observation(1, 0, true, false, false, 1, 1);
        await using var session = new TemporalGateSession("composition", TimeSpan.FromSeconds(2), 8, 8, BaseUtc);
        Assert(session.TryAdd(observation, null, out _), "Observation rejected.");

        var all = new AllOfGate("all", new ITemporalGate[]
        {
            new FixedGate("a", TemporalGateState.Passed),
            new FixedGate("b", TemporalGateState.Passed)
        });
        Assert(all.Observe(session, observation).GateState == TemporalGateState.Passed, "AllOf did not require/pass all gates.");

        var any = new AnyOfGate("any", new ITemporalGate[]
        {
            new FixedGate("c", TemporalGateState.Rejected),
            new FixedGate("d", TemporalGateState.Passed)
        });
        Assert(any.Observe(session, observation).GateState == TemporalGateState.Passed, "AnyOf did not pass one successful child.");

        var sequence = new SequenceGate("sequence", new ITemporalGate[]
        {
            new FixedGate("e", TemporalGateState.Passed),
            new FixedGate("f", TemporalGateState.Passed)
        });
        Assert(sequence.Observe(session, observation).GateState == TemporalGateState.Pending, "Sequence completed too early.");
        Assert(sequence.Observe(session, observation with { FrameId = new FrameId(2), UtcTimestamp = BaseUtc.AddMilliseconds(100) }).GateState == TemporalGateState.Passed, "Sequence did not enforce order.");

        var faulted = new AllOfGate("fault", new ITemporalGate[]
        {
            new FixedGate("g", TemporalGateState.Passed),
            new FixedGate("h", TemporalGateState.Faulted)
        });
        Assert(faulted.Observe(session, observation).GateState == TemporalGateState.Faulted, "Faulted composite incorrectly passed.");
    }

    private static async Task EngineTerminationIsPreciseAsync()
    {
        var profile = BuiltInGateProfiles.StableHeaderAndPanel;
        var timeoutEngine = new TemporalGateEngine(profile, new TemporalGateEngineOptions { MaximumDuration = TimeSpan.FromMilliseconds(40) });
        await using (var run = await timeoutEngine.RunAsync(new NeverFrameSource()).ConfigureAwait(false))
        {
            Assert(run.Result.GateState == TemporalGateState.TimedOut, "No-frame timeout did not time out.");
            Assert(run.Result.ReasonCode == GateReasonCode.NoFramesReceived, "No-frame timeout reason is wrong.");
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(30));
        var cancelEngine = new TemporalGateEngine(profile, new TemporalGateEngineOptions { MaximumDuration = TimeSpan.FromSeconds(1) });
        await using (var run = await cancelEngine.RunAsync(new NeverFrameSource(), cts.Token).ConfigureAwait(false))
        {
            Assert(run.Result.GateState == TemporalGateState.Cancelled, "Cancellation did not propagate.");
            Assert(run.Result.ReasonCode == GateReasonCode.Cancelled, "Cancellation reason is wrong.");
        }

        var faultEngine = new TemporalGateEngine(profile, new TemporalGateEngineOptions { MaximumDuration = TimeSpan.FromSeconds(1) });
        await using (var run = await faultEngine.RunAsync(new ThrowingFrameSource()).ConfigureAwait(false))
        {
            Assert(run.Result.GateState == TemporalGateState.Faulted, "Fault did not propagate.");
            Assert(run.Result.ReasonCode == GateReasonCode.Faulted, "Fault reason is wrong.");
        }
    }

    private static async Task FreezeDetectionIsTemporalAsync()
    {
        var profile = BuiltInGateProfiles.StableHeaderAndPanel with
        {
            Observer = BuiltInGateProfiles.StableHeaderAndPanel.Observer with
            {
                FrozenSourceTimestampFrames = 3,
                FreezeIntervalThreshold = TimeSpan.FromSeconds(5)
            }
        };
        await using var observer = new MultiRegionTemporalObserver(profile.Regions, profile.Observer);
        TemporalFrameObservation? last = null;
        for (var index = 0; index < 5; index++)
        {
            using var retained = new RetainedFrame(CreatePixelLease(index + 1, sourceTicks: 10, milliseconds: index * 100, modelVariant: 0, requiredVariant: 0));
            last = await observer.AnalyzeAsync(retained).ConfigureAwait(false);
        }

        Assert(last is not null && (last.QualityFlags & TemporalQualityFlags.StreamFrozen) != 0, "Repeated source timestamp/fingerprint did not trigger freeze detection.");
    }

    private static Task RoiViewRespectsStrideAsync()
    {
        const int width = 4;
        const int height = 3;
        const int stride = 20;
        var pixels = new byte[stride * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                pixels[(y * stride) + (x * 4)] = (byte)(10 * y + x);
            }
        }

        var lease = new TestFrameLease(
            new FrameMetadata(
                new FrameId(1),
                new FrameTimestamp(1, BaseUtc, TimeSpan.Zero),
                new FrameDescriptor(width, height, stride, FramePixelFormat.Bgra32),
                FrameQuality.Unknown,
                new FrameStability(0, 1, TimeSpan.Zero, true),
                "test"),
            pixels);
        using (var crop = new FrameCropView(lease, new NormalizedRegion(0.25, 1d / 3d, 0.50, 1d / 3d)))
        {
            Assert(crop.Width == 2 && crop.Height == 1, "ROI dimensions are wrong.");
            Assert(crop.GetRow(0)[0] == 11, "ROI did not point at the expected pixel.");
        }

        Assert(lease.IsDisposed, "ROI did not retain/release its lease correctly.");
        AssertThrows<ArgumentOutOfRangeException>(() => new NormalizedRegion(-0.1, 0, 1, 1).Validate(), "Out-of-range ROI was accepted.");
        return Task.CompletedTask;
    }

    private static Task PublicSurfaceIsReadOnlyAsync()
    {
        Assert(!ScrcpyReadOnlyContract.ControlChannelEnabled, "Control channel contract is enabled.");
        Assert(ScrcpyReadOnlyContract.InputCommandsSent == 0, "Read-only contract reports input commands.");
        var forbidden = new[] { "tap", "swipe", "scroll", "keypress", "keyevent", "clipboard", "textinput", "home", "back", "power", "rotate" };
        var assemblies = new[] { typeof(ScrcpyReadOnlyVideoTransport).Assembly, typeof(TemporalGateEngine).Assembly };
        var publicMethods = assemblies
            .SelectMany(x => x.GetExportedTypes())
            .SelectMany(x => x.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.DeclaredOnly))
            .Select(x => $"{x.DeclaringType?.FullName}.{x.Name}")
            .ToArray();
        foreach (var method in publicMethods)
        {
            Assert(!forbidden.Any(token => method.Contains(token, StringComparison.OrdinalIgnoreCase)), $"Forbidden input-like public API found: {method}");
        }

        return Task.CompletedTask;
    }

    private static async Task GateEvaluationIsDeterministicAsync()
    {
        var first = await RunGateAsync(GateFactory.Create(BuiltInGateProfiles.GenericScreenTransition), MeaningfulTransitionSequence()).ConfigureAwait(false);
        var second = await RunGateAsync(GateFactory.Create(BuiltInGateProfiles.GenericScreenTransition), MeaningfulTransitionSequence()).ConfigureAwait(false);
        Assert(first.GateState == second.GateState, "Gate state is non-deterministic.");
        Assert(first.ReasonCode == second.ReasonCode, "Gate reason is non-deterministic.");
        Assert(first.SelectedEvidenceFrameIds.SequenceEqual(second.SelectedEvidenceFrameIds), "Evidence selection is non-deterministic.");
    }

    private static IEnumerable<TemporalFrameObservation> MeaningfulTransitionSequence()
    {
        yield return Observation(1, 0, true, true, false, 0x1111111111111111UL, 1);
        yield return Observation(2, 100, true, true, false, 0x1111111111111111UL, 2);
        yield return Observation(3, 200, true, true, false, 0x1111111111111111UL, 3);
        yield return Observation(4, 300, false, true, true, 0x2222222222222222UL, 4);
        yield return Observation(5, 400, false, true, true, 0x3333333333333333UL, 5);
        yield return Observation(6, 500, true, true, false, 0xeeeeeeeeeeeeeeeeUL, 6);
        yield return Observation(7, 600, true, true, false, 0xeeeeeeeeeeeeeeeeUL, 7);
        yield return Observation(8, 700, true, true, false, 0xeeeeeeeeeeeeeeeeUL, 8);
        yield return Observation(9, 800, true, true, false, 0xeeeeeeeeeeeeeeeeUL, 9);
    }

    private static async Task<TemporalGateResult> RunGateAsync(
        ITemporalGate gate,
        IEnumerable<TemporalFrameObservation> observations,
        GateTermination incompleteTermination = GateTermination.StreamEnded)
    {
        await using var session = new TemporalGateSession(gate.Name, TimeSpan.FromSeconds(20), 64, 128, BaseUtc);
        TemporalGateResult? result = null;
        foreach (var observation in observations)
        {
            Assert(session.TryAdd(observation, null, out var rejection), $"Observation {observation.FrameId} rejected: {rejection}.");
            result = gate.Observe(session, observation);
            if (result.IsTerminal)
            {
                session.TryComplete(result);
                return result;
            }
        }

        result = gate.Complete(session, incompleteTermination, observations.LastOrDefault()?.UtcTimestamp ?? BaseUtc);
        session.TryComplete(result);
        return result;
    }

    private static TemporalFrameObservation Observation(
        long frameId,
        int milliseconds,
        bool requiredStable,
        bool volatileMoving,
        bool transition,
        ulong requiredFingerprint,
        ulong fullFingerprint)
    {
        var requiredMotion = requiredStable ? 0.01 : transition ? 0.25 : 0.20;
        var requiredDifference = requiredStable ? 0.01 : transition ? 0.20 : 0.15;
        var requiredSimilarity = requiredStable ? 0.99 : 0.60;
        var requiredSharpness = 0.80;
        var volatileMotion = volatileMoving ? 0.45 : 0.01;
        var volatileDifference = volatileMoving ? 0.35 : 0.01;
        var volatileSimilarity = volatileMoving ? 0.55 : 0.99;
        var regions = new Dictionary<string, RegionalFrameObservation>(StringComparer.Ordinal)
        {
            ["FullFrame"] = Region("FullFrame", RegionStabilityRole.DiagnosticOnly, false, volatileMoving ? 0.30 : requiredDifference, volatileMoving ? 0.40 : requiredMotion, volatileMoving ? 0.60 : requiredSimilarity, 0.75, fullFingerprint),
            ["Header"] = Region("Header", RegionStabilityRole.Required, true, requiredDifference, requiredMotion, requiredSimilarity, requiredSharpness, requiredFingerprint ^ 0x0101010101010101UL),
            ["Model"] = Region("Model", RegionStabilityRole.Volatile, false, volatileDifference, volatileMotion, volatileSimilarity, 0.70, fullFingerprint ^ 0x0202020202020202UL),
            ["AnimatedBackground"] = Region("AnimatedBackground", RegionStabilityRole.Volatile, false, volatileDifference, volatileMotion, volatileSimilarity, 0.65, fullFingerprint ^ 0x0303030303030303UL),
            ["Panel"] = Region("Panel", RegionStabilityRole.Required, true, requiredDifference, requiredMotion, requiredSimilarity, requiredSharpness, requiredFingerprint ^ 0x0404040404040404UL),
            ["BottomControl"] = Region("BottomControl", RegionStabilityRole.Required, true, requiredDifference, requiredMotion, requiredSimilarity, requiredSharpness, requiredFingerprint ^ 0x0808080808080808UL)
        };
        return new TemporalFrameObservation
        {
            FrameId = new FrameId(frameId),
            SourceTicks = frameId,
            MonotonicTimestamp = TimeSpan.FromMilliseconds(milliseconds),
            UtcTimestamp = BaseUtc.AddMilliseconds(milliseconds),
            FrameInterval = frameId == 1 ? null : TimeSpan.FromMilliseconds(100),
            GlobalDifferenceScore = regions["FullFrame"].DifferenceScore,
            RegionalDifferenceScores = regions.ToDictionary(x => x.Key, x => x.Value.DifferenceScore, StringComparer.Ordinal),
            MotionScore = regions["FullFrame"].MotionScore,
            SharpnessScore = regions["FullFrame"].SharpnessScore,
            FreezeScore = 0,
            BrightnessScore = 0.5,
            ContrastScore = 0.6,
            Resolution = new FrameResolution(80, 120),
            IsLikelyStable = requiredStable,
            IsLikelyTransitioning = transition,
            QualityFlags = TemporalQualityFlags.None,
            Regions = regions,
            VisualFingerprint = fullFingerprint,
            ObservationDuration = TimeSpan.FromMilliseconds(1)
        };
    }

    private static RegionalFrameObservation Region(
        string name,
        RegionStabilityRole role,
        bool observeTransition,
        double difference,
        double motion,
        double similarity,
        double sharpness,
        ulong fingerprint) => new()
    {
        RegionName = name,
        StabilityRole = role,
        ObserveTransition = observeTransition,
        DifferenceScore = difference,
        MotionScore = motion,
        SimilarityScore = similarity,
        SharpnessScore = sharpness,
        BrightnessScore = 0.5,
        ContrastScore = 0.6,
        ChangeVelocity = difference,
        VisualFingerprint = fingerprint,
        IsLikelyStable = motion <= 0.05 && difference <= 0.04 && similarity >= 0.94 && sharpness >= 0.18,
        IsLikelyTransitioning = observeTransition && (motion >= 0.08 || difference >= 0.07)
    };

    private static TemporalFrameObservation WithSharpness(TemporalFrameObservation observation, double header, double panel, double model)
    {
        var regions = observation.Regions.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
        regions["Header"] = regions["Header"] with { SharpnessScore = header };
        regions["Panel"] = regions["Panel"] with { SharpnessScore = panel };
        regions["Model"] = regions["Model"] with { SharpnessScore = model };
        return observation with { Regions = regions };
    }

    private static IFrameLease CreatePixelLease(long id, long sourceTicks, int milliseconds, int modelVariant, int requiredVariant)
    {
        const int width = 80;
        const int height = 120;
        const int stride = width * 4;
        var pixels = new byte[stride * height];
        FillRegion(pixels, width, height, stride, new NormalizedRegion(0, 0, 1, 1), (_, _) => (byte)(modelVariant == 0 ? 30 : 180));
        FillRegion(pixels, width, height, stride, new NormalizedRegion(0.10, 0.02, 0.80, 0.14), (x, y) => (byte)(((x + y + requiredVariant) & 1) == 0 ? 20 : 240));
        FillRegion(pixels, width, height, stride, new NormalizedRegion(0.05, 0.55, 0.90, 0.32), (x, y) => (byte)(((x + y + requiredVariant) & 1) == 0 ? 15 : 245));
        FillRegion(pixels, width, height, stride, new NormalizedRegion(0.05, 0.87, 0.90, 0.11), (x, y) => (byte)(((x + y + requiredVariant) & 1) == 0 ? 25 : 230));
        FillRegion(pixels, width, height, stride, new NormalizedRegion(0.10, 0.16, 0.80, 0.39), (x, y) => (byte)((x + y + (modelVariant * 79)) % 256));
        return new TestFrameLease(
            new FrameMetadata(
                new FrameId(id),
                new FrameTimestamp(sourceTicks, BaseUtc.AddMilliseconds(milliseconds), TimeSpan.FromMilliseconds(milliseconds)),
                new FrameDescriptor(width, height, stride, FramePixelFormat.Bgra32),
                FrameQuality.Unknown,
                new FrameStability(0, 0, TimeSpan.Zero, false),
                "synthetic"),
            pixels);
    }

    private static void FillRegion(
        byte[] pixels,
        int width,
        int height,
        int stride,
        NormalizedRegion region,
        Func<int, int, byte> luma)
    {
        var x0 = Math.Clamp((int)Math.Floor(region.X * width), 0, width - 1);
        var y0 = Math.Clamp((int)Math.Floor(region.Y * height), 0, height - 1);
        var x1 = Math.Clamp((int)Math.Ceiling((region.X + region.Width) * width), x0 + 1, width);
        var y1 = Math.Clamp((int)Math.Ceiling((region.Y + region.Height) * height), y0 + 1, height);
        for (var y = y0; y < y1; y++)
        {
            for (var x = x0; x < x1; x++)
            {
                var value = luma(x, y);
                var offset = (y * stride) + (x * 4);
                pixels[offset] = value;
                pixels[offset + 1] = value;
                pixels[offset + 2] = value;
                pixels[offset + 3] = 255;
            }
        }
    }

    private static IFrameLease CreateSolidLease(long id, DateTimeOffset timestamp, byte value)
    {
        var pixels = Enumerable.Repeat(value, 4 * 4 * 4).ToArray();
        return new TestFrameLease(
            new FrameMetadata(
                new FrameId(id),
                new FrameTimestamp(id, timestamp, timestamp - BaseUtc),
                new FrameDescriptor(4, 4, 16, FramePixelFormat.Bgra32),
                FrameQuality.Unknown,
                new FrameStability(0, 1, TimeSpan.Zero, true),
                "test"),
            pixels);
    }

    private static FrameMetadata Metadata(long id, int milliseconds) => new(
        new FrameId(id),
        new FrameTimestamp(id, BaseUtc.AddMilliseconds(milliseconds), TimeSpan.FromMilliseconds(milliseconds)),
        new FrameDescriptor(2, 2, 8, FramePixelFormat.Bgra32),
        FrameQuality.Unknown,
        new FrameStability(0, 1, TimeSpan.Zero, true),
        "test");

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void AssertThrows<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }

    private sealed class TestFrameLease : IFrameLease
    {
        private byte[]? _pixels;

        public TestFrameLease(FrameMetadata metadata, byte[] pixels)
        {
            Metadata = metadata;
            _pixels = pixels;
        }

        public FrameMetadata Metadata { get; }
        public bool IsDisposed => _pixels is null;
        public ReadOnlyMemory<byte> Pixels => _pixels ?? throw new ObjectDisposedException(nameof(TestFrameLease));
        public void Dispose() => Interlocked.Exchange(ref _pixels, null);
    }

    private sealed class FixedGate : ITemporalGate
    {
        private readonly TemporalGateState _state;

        public FixedGate(string name, TemporalGateState state)
        {
            Name = name;
            _state = state;
        }

        public string Name { get; }

        public TemporalGateResult Observe(TemporalGateSession session, TemporalFrameObservation observation) =>
            _state == TemporalGateState.Pending
                ? TemporalGateResult.Pending(Name, session, GateReasonCode.Pending, 0)
                : TemporalGateResult.Terminal(
                    Name,
                    session,
                    _state,
                    _state == TemporalGateState.Passed ? GateReasonCode.Passed : GateReasonCode.Faulted,
                    _state == TemporalGateState.Passed ? 1 : 0,
                    observation.UtcTimestamp);

        public TemporalGateResult Complete(TemporalGateSession session, GateTermination termination, DateTimeOffset timestamp, Exception? error = null) =>
            GateEvaluationProxy.Complete(Name, session, termination, timestamp, error);
    }

    private static class GateEvaluationProxy
    {
        public static TemporalGateResult Complete(string name, TemporalGateSession session, GateTermination termination, DateTimeOffset timestamp, Exception? error) =>
            termination switch
            {
                GateTermination.Timeout => TemporalGateResult.Terminal(name, session, TemporalGateState.TimedOut, GateReasonCode.ObservationTimedOut, 0, timestamp),
                GateTermination.Cancelled => TemporalGateResult.Terminal(name, session, TemporalGateState.Cancelled, GateReasonCode.Cancelled, 0, timestamp),
                GateTermination.StreamEnded => TemporalGateResult.Terminal(name, session, TemporalGateState.Rejected, GateReasonCode.InsufficientEvidence, 0, timestamp),
                GateTermination.Faulted => TemporalGateResult.Terminal(name, session, TemporalGateState.Faulted, GateReasonCode.Faulted, 0, timestamp, new Dictionary<string, object?> { ["Error"] = error?.Message }),
                _ => throw new ArgumentOutOfRangeException(nameof(termination))
            };
    }

    private sealed class NeverFrameSource : IFrameLeaseSource
    {
        public long DroppedFrames => 0;
        public IAsyncEnumerable<IFrameLease> ReadAsync(CancellationToken cancellationToken = default) => Never(cancellationToken);

        private static async IAsyncEnumerable<IFrameLease> Never([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            yield break;
        }
    }

    private sealed class ThrowingFrameSource : IFrameLeaseSource
    {
        public long DroppedFrames => 0;
        public IAsyncEnumerable<IFrameLease> ReadAsync(CancellationToken cancellationToken = default) => Throw(cancellationToken);

        private static async IAsyncEnumerable<IFrameLease> Throw([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            if (DateTime.UtcNow.Ticks >= 0)
            {
                throw new InvalidOperationException("Synthetic frame-source failure.");
            }

            yield break;
        }
    }
}
