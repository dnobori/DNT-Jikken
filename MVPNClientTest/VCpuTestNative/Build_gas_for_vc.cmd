@echo off

c:\windows\sysnative\bash.exe Build_gas_for_vc.sh

if not %ERRORLEVEL% == 0 (
 echo Build_gas_for_vc.sh error.
 exit /b 1
)

