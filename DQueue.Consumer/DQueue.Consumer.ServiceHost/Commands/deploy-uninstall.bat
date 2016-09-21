@echo off

echo ---------- Uninstall DQueue.Consumer.Service ----------
%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\InstallUtil.exe -u %~dp0\..\DQueue.Consumer.ServiceHost.exe

pause