dotnet build "$PSScriptRoot\..\src\PogoInventory.Streaming.Preflight\PogoInventory.Streaming.Preflight.csproj" -c Release
dotnet build "$PSScriptRoot\..\tests\PogoInventory.Streaming.Phase4.SelfTest\PogoInventory.Streaming.Phase4.SelfTest.csproj" -c Release
exit $LASTEXITCODE
