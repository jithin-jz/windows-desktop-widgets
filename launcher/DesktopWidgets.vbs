' Launches the desktop widgets with no console window and no taskbar flash.
' The Startup shortcut points here, so this file is the single place that
' decides what actually runs.
'
' Layout expected (as the installer lays it out):
'   <install>\launcher\DesktopWidgets.vbs   <- this file
'   <install>\bin\DesktopWidgets.exe
'   <install>\build.cmd

Set fso = CreateObject("Scripting.FileSystemObject")
Set sh = CreateObject("WScript.Shell")

launcherDir = fso.GetParentFolderName(WScript.ScriptFullName)
rootDir = fso.GetParentFolderName(launcherDir)
exePath = fso.BuildPath(rootDir, "bin\DesktopWidgets.exe")

If fso.FileExists(exePath) Then
    sh.Run """" & exePath & """", 0, False
Else
    ' Not built yet. Build once, hidden, then start. This makes a fresh clone
    ' runnable without the installer, and recovers the case where bin\ was
    ' cleaned but the sources are intact.
    buildPath = fso.BuildPath(rootDir, "build.cmd")
    If fso.FileExists(buildPath) Then
        sh.Run "cmd.exe /c """ & buildPath & """", 0, True
        If fso.FileExists(exePath) Then
            sh.Run """" & exePath & """", 0, False
        Else
            MsgBox "Desktop Widgets could not be built." & vbCrLf & vbCrLf & _
                   "Run build.cmd from a terminal to see the compiler output:" & vbCrLf & _
                   buildPath, 16, "Desktop Widgets"
        End If
    Else
        MsgBox "Desktop Widgets is not installed correctly." & vbCrLf & vbCrLf & _
               "Expected to find:" & vbCrLf & exePath, 16, "Desktop Widgets"
    End If
End If
