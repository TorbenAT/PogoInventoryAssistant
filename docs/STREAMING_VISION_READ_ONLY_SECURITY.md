# Read-only sikkerhedsgrænse

Fase 3 observerer kun videostrømmen.

Der findes ingen offentlige gate- eller streammetoder til:

- tap
- swipe
- scroll
- back
- home
- key events
- text input
- clipboard
- power
- rotation

Scrcpy-starten indeholder eksplicit:

```text
audio=false
control=false
raw_stream=true
```

Der oprettes én video-forward. Der oprettes ingen control channel.

`ScrcpyReadOnlyContract` fastslår:

```text
ControlChannelEnabled = false
InputCommandsSent = 0
```

Self-testen reflekterer de offentlige assemblies og afviser inputlignende metoder.
