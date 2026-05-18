# How to Set up a Local Nuget feed to Use a Custom Build of Halibut in Tentacle and Octopus

It's time consuming to rely on Teamcity pull requests to generate artifacts to line up versions of Halibut, Tentacle and Octopus to develop locally.

Instead, we can build and generate local packages, and use them directly using the Local Nuget Feed feature.

These instructions are for Rider, but the Local Nuget feed feature is not IDE-dependant.

## Halibut

1. Open the Halibut solution in Rider.
2. Go to the "\_build" run configuration and set the Program Arguments to `--Target CopyToLocalPackages`
3. Run the "\_build" configuration.
4. This will generate some artifacts in C:\dev\LocalPackages. (This is my local packages folder - your output folder may be different. Please see the Run output)
5. The artifacts will contain files that are suffixed with your branch name, e.g. Halibut.8.1.1234-branch-name
6. Run the command `dotnet nuget add source C:\dev\LocalPackages\ -n "Local Packages"`. Replace the path with your local output folder
7. Open the Tentacle Solution in Rider
8. Verify that Rider is showing `C:\dev\LocalPackages\` as a new feed in the Nuget section.
9. In the Tentacle repository, use Rider's Nuget > Packages section to select the correct Version of Halibut. Ensure that the "Prerelease" checkbox is checked.
10. Click on the "upgrade"/"downgrade" icon.
11. Build Tentacle. You should now be using the local version of Halibut.

## Tentacle

1.  Open the Tentacle solution in Rider.
2.  Go to the "\_build" run configuration and set the Program Arguments to `--Target CopyClientAndContractsToLocalPackages`
3.  Run the "\_build" configuration.
4.  This will generate some artifacts in C:\dev\LocalPackages. (This is my local packages folder - your output folder may be different. Please see the Run output)
5.  The artifacts will contain files that are suffixed with your branch name, e.g. Octopus.Tentacle.Contracts.8.3.2786-branch-name and Octopus.Tentacle.Client.8.3.2786-branch-name
6.  Open the Octopus Solution in Rider
7.  Verify that Rider is showing `C:\dev\LocalPackages\` as a new feed in the Nuget section.
8.  In the Octopus repository, use Rider's Nuget > Packages section to select the correct Version of Tentacle.Client and Tentacle.Contracts. Ensure that the "Prerelease" checkbox is checked.
9.  Click on the "upgrade"/"downgrade" icon.
10. Build Octopus. You should now be using the local version of Tentacle.
