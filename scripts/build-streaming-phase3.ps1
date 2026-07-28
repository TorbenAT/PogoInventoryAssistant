$ErrorActionPreference = 'Stop'
dotnet build "$PSScriptRoot\..\src\PogoInventory.Streaming.Gates\PogoInventory.Streaming.Gates.csproj" -c Release
dotnet build "$PSScriptRoot\..\src\PogoInventory.Streaming.Observe.Gates\PogoInventory.Streaming.Observe.Gates.csproj" -c Release
