@echo off

echo ---------- Install DQueue.Central.Service ----------
%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\InstallUtil.exe -i %~dp0\..\DQueue.Central.ServiceHost.exe

echo ---------- Start DQueue.Central.Service ----------
net start DQueue.Central.Service

pause