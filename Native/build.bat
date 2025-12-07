@echo off
setlocal

echo === Building DuckovGuard Native Library ===

if not exist build mkdir build
cd build

cmake .. -G "Visual Studio 17 2022" -A x64
if %errorlevel% neq 0 (
    echo CMake configuration failed, trying MinGW...
    cmake .. -G "MinGW Makefiles"
)

cmake --build . --config Release

if exist Release\duckov_guard.dll (
    copy /Y Release\duckov_guard.dll ..\..\bin\Release\net8.0\
    echo DLL copied to bin\Release\net8.0\
)

if exist duckov_guard.dll (
    copy /Y duckov_guard.dll ..\..\bin\Release\net8.0\
    echo DLL copied to bin\Release\net8.0\
)

echo === Build Complete ===
