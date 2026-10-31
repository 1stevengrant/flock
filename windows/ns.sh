set -euo pipefail

git diff --quiet
git diff --cached --quiet

git mv FlockKit Flock.Kit
git mv FlockKit.Tests Flock.Kit.Tests
git mv FlockTray Flock.Tray
git mv FlockTray.Tests Flock.Tray.Tests
git mv FlockScreenshots Flock.Screenshots

git mv Flock.Kit/FlockKit.csproj Flock.Kit/Flock.Kit.csproj
git mv Flock.Kit.Tests/FlockKit.Tests.csproj Flock.Kit.Tests/Flock.Kit.Tests.csproj
git mv Flock.Tray/FlockTray.csproj Flock.Tray/Flock.Tray.csproj
git mv Flock.Tray.Tests/FlockTray.Tests.csproj Flock.Tray.Tests/Flock.Tray.Tests.csproj
git mv Flock.Screenshots/FlockScreenshots.csproj Flock.Screenshots/Flock.Screenshots.csproj

git status --short
git diff --cached --summary