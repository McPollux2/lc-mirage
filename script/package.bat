@echo off

call ./build.bat
mkdir "../bin/core"
mkdir "../bin/plugins"

powershell Move-Item -Path "../bin/Mirage.dll" -Destination "../bin/plugins/Mirage.dll"
powershell Copy-Item -Path "../lib/FSharp.Control.AsyncSeq.dll" -Destination "../bin/core/FSharp.Control.AsyncSeq.dll"
powershell Copy-Item -Path "../lib/FSharp.Core.dll" -Destination "../bin/core/FSharp.Core.dll"
powershell Copy-Item -Path "../lib/FSharpPlus.dll" -Destination "../bin/core/FSharpPlus.dll"
powershell Copy-Item -Path "../lib/FSharpx.Async.dll" -Destination "../bin/core/FSharpx.Async.dll"
powershell Copy-Item -Path "../lib/libmp3lame.64.dll" -Destination "../bin/core/libmp3lame.64.dll"
powershell Copy-Item -Path "../lib/NAudio.Asio.dll" -Destination "../bin/core/NAudio.Asio.dll"
powershell Copy-Item -Path "../lib/NAudio.Core.dll" -Destination "../bin/core/NAudio.Core.dll"
powershell Copy-Item -Path "../lib/NAudio.dll" -Destination "../bin/core/NAudio.dll"
powershell Copy-Item -Path "../lib/NAudio.Lame.dll" -Destination "../bin/core/NAudio.Lame.dll"
powershell Copy-Item -Path "../lib/NAudio.Midi.dll" -Destination "../bin/core/NAudio.Midi.dll"
powershell Copy-Item -Path "../lib/NAudio.Wasapi.dll" -Destination "../bin/core/NAudio.Wasapi.dll"
powershell Copy-Item -Path "../lib/NAudio.WinForms.dll" -Destination "../bin/core/NAudio.WinForms.dll"
powershell Copy-Item -Path "../lib/NAudio.WinMM.dll" -Destination "../bin/core/NAudio.WinMM.dll"

powershell Compress-Archive^
    -Force^
    -Path "../bin/core",^
          "../bin/plugins",^
          "../manifest.json",^
          "../icon.png",^
          "../README.md",^
          "../LICENSE"^
    -DestinationPath "../bin/mirage.zip"
