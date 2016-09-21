@echo off

echo ---------- Install DQueue.Consumer.Service ----------
%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\InstallUtil.exe -i %~dp0\..\DQueue.Consumer.ServiceHost.exe

echo ---------- Start DQueue.Consumer.Service ----------
net start DQueue.Consumer.Service

pause