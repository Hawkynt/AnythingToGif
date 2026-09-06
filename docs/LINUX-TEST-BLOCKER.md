# Linux Test Support Investigation

## Summary

Attempts to enable Linux support for the test project revealed a **blocking dependency** that prevents tests from running on Linux without significant code changes.

## The Blocker

The project depends on `FrameworkExtensions.System.Drawing` (version 1.0.0.36), which has a hard requirement for the `Microsoft.WindowsDesktop.App.WindowsForms` framework. This framework is **Windows-only** and cannot be installed or satisfied on Linux.

The error when attempting to build on Linux:
```
error NETSDK1073: The FrameworkReference 'Microsoft.WindowsDesktop.App.WindowsForms' was not recognized
```

## What Was Attempted

1. ✗ **Changed target framework** from `net8.0-windows` to `net8.0`
   - Result: Dependency resolution fails for Windows Forms

2. ✗ **Multi-targeting** both `net8.0` and `net8.0-windows`
   - Result: Still pulls in Windows Forms dependency

3. ✗ **Conditional package inclusion** based on target framework
   - Result: Causes compilation errors (missing BitmapExtensions APIs)

4. ✗ **Platform check overrides** (`<NoPlatformCheck>true</NoPlatformCheck>`)
   - Result: Doesn't bypass framework reference resolution

5. ✗ **Excluding framework references** via MSBuild properties
   - Result: MSBuild still resolves and requires them

6. ✗ **Removing the package** entirely
   - Result: 33 compilation errors (BitmapExtensions namespace missing)

## The Dependency Chain

```
AnythingToGif.Tests
  └─> AnythingToGif
      └─> FrameworkExtensions.System.Drawing (1.0.0.36)
          └─> Microsoft.WindowsDesktop.App.WindowsForms (Windows-only)
```

The `BitmapExtensions` class methods from `System.Drawing.BitmapExtensions` namespace are used extensively throughout the dithering code (33+ references).

## Options for Linux Support

### Option 1: Contact Package Author
Ask the maintainer of `FrameworkExtensions.System.Drawing` to:
- Remove the Windows Forms dependency if not actually needed
- Create a cross-platform version of the package
- Package: https://www.nuget.org/packages/FrameworkExtensions.System.Drawing/

### Option 2: Replace the Dependency
Find or create a cross-platform alternative that provides:
- `BitmapExtensions` functionality
- Bitmap locking/unlocking APIs
- The specific extension methods used by ditherers

This would require:
- Identifying all APIs used from the package
- Finding/creating cross-platform equivalents
- Refactoring ~30+ ditherer files

### Option 3: Fork and Modify
- Fork the `FrameworkExtensions` library
- Remove Windows Forms dependency
- Rebuild as cross-platform package
- Reference the forked version

### Option 4: Accept Windows-Only Tests
Document that tests currently only run on Windows due to the dependency constraint.

## Current State

The test project remains at `net8.0-windows` target framework. The GitHub Actions CI workflow includes a test job, but it will **not succeed** until the Windows Forms dependency is resolved.

## Recommendation

Contact the package author first (Option 1) as it's the least intrusive solution. If that's not viable, Option 2 (replace dependency) would provide the most maintainable long-term solution but requires significant refactoring effort.
