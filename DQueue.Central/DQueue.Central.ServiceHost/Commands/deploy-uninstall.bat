@echo off

echo ---------- Uninstall DQueue.Central.Service ----------
%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\InstallUtil.exe -u %~dp0\..\DQueue.Central.ServiceHost.exe

pause