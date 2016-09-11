@echo off

echo ---------- Uninstall DQueue.Service ----------
%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\InstallUtil.exe -u %~dp0\..\DQueue.ServiceHost.exe

pause