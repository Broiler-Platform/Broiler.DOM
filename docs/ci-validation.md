# CI and package validation — 2026-09-12

Changes were validated locally across DOM, Graphics, Input, Media, and Native.
No commits, tags, workflow runs, or package releases were pushed.

| Repository | Verified packages | Windows validation | Fresh consumer restore |
| --- | ---: | --- | --- |
| DOM | 2 | Build; 167 tests | Local artifacts + NuGet.org |
| Graphics | 7 | Build; all four host-applicable suites | Local artifacts + GitHub Packages |
| Input | 23 | Build; Android and Windows contract suites | Local artifacts + GitHub Packages |
| Media | 9 | Build; all seven suites | Local artifacts + GitHub Packages |
| Native | 5 | Build; all 9 checks | Local artifacts + NuGet.org |

All packages were built with the temporary validation version
`0.1.0-preview.999999`. Archive checks verified package identities, internal
dependency versions, README, icon, runtime assemblies, API documentation, and
symbol packages. Dependency-only meta-packages do not require symbols.
Output went to temporary directories, outside the repositories.

All ten workflows passed actionlint 1.7.12. Version-selection tests passed, and
live read-only feed lookups selected the next previews: DOM 1, Graphics 3,
Input 5, Media 12, Native 4. These values are observations, not reservations.
Invalid versions and stale package output were rejected before packing.

The additional destination-feed check correctly rejected NuGet.org releases of
Graphics, Input, and Media because their required upstream Broiler packages are
not available there. Publish Native first, then Media, before those consumers.
No repository-level `NUGET_API_KEY` secret was present in any of the five
repositories. Organization-level secrets could not be inspected with the current
GitHub CLI credentials; Publish checks the effective secret before a real push.

Linux execution remains to be confirmed on GitHub's Ubuntu runner. Both local
WSL distributions failed to start because their registered virtual disks are
missing. The workflows include Linux builds and host-specific suites; this report
does not claim those jobs have run successfully. Input and Graphics Linux test
projects compile on Windows; Graphics reports the existing CS8602 warning in
`LinuxOpenGlNativeReplay.cs:25`.

The Input Windows suite exposed an obsolete source-fallback dependency guard that
had been skipped by the previous CI. It now verifies centrally declared Native
dependencies, permits the existing camera-specific Media dependencies only in
the camera provider, and rejects dependencies conditional on sibling checkouts.

The pre-existing untracked `.bak` files in Input and Media were preserved.
