@echo off

echo ---------- Install DQueue.Service ----------
%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\InstallUtil.exe -i %~dp0\..\DQueue.ServiceHost.exe

echo ---------- Start DQueue.Service ----------
net start DQueue.Service

pause