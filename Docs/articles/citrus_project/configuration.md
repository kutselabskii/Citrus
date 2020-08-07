# Citrus Project Configuration

Citrus project configuration is provided via a JSON (currently Newtonsoft.Json parseable) file with extension `.citproj`. 

`ProjectDirectory` is considered to be a directory where project configuration file is located. Whenever it is stated that path is relative, implied it's relative to `ProjectDirectory` unless stated otherwise.

## Root Level

Property                          | Type                                      | Description
----------------------------------|-------------------------------------------|-------------------------------------------------------------------------------------------------------------------------------
`Name`                            | string                                    | Project name. Orange expects it to appear in [directory structure](directory_structure.md) as described.
`AssetsDirectory`                 | string                                    | can be used to override default Assets Directory `./Data/`
`GeneratedScenesPath`             | string                                    | Path for scene code generated by Kumqat. Default is `GeneratedScenes`
`DontSynchronizeProject`          | bool                                      | Used to disable project synchronization which is enabled by default.
`DictionariesPath`                | string                                    | Path to dictionaries directory relative to assets directory. Default is `Lime.Localization.DictionariesPath = "Localization"`.
`UnresolvedAssembliesDirectory`   | string                                    | default:  `$"{ProjectName}.OrangePlugin/bin/$(CONFIGURATION)/"`
`GeneratedDeserializerPath`       | string                                    | Path to save yuzu generated binary deserializers for application types if used.
`CitrusDirectory`                 | string                                    | relative path to Citrus engine directory. Default is `./Citrus/`. Should be set if path deviates from the default.
`SkipAssetsCooking`               | bool                                      | Option to omit assets cooking.
`SkipCodeCooking`                 | bool                                      | Option to omit Kumquat code generation. Should be set to `true` if project doesn't use Kumquat.
`RawAssetExtensions`              | string                                    | Space separated extensions for files to be treated as raw assets in form `.xxx .yyy .zzz...`
`LocalizeOnlyTaggedSceneTexts`    | string                                    | TODO
`AddContextToLocalizedDictionary` | bool                                      | TODO
`Targets`                         | List<[Target](#target)>                   | Lists user defined targets.
`XCodeProject`                    | [XCodeProject](#xcodeproject)             | Setting specific to XCodePproject generation.
`PluginAssemblies`                | [PluginAssemblies](#pluginassemblies)     | Describes which assemblies should be loaded by Orange/Tangerine.
`AssetCache`                      | [AssetCache](#assetcache)                 | Asset Cache settings
`RemoteScripting`                 | [RemoteScripting](#remotescripting)       | TODO
`ResolutionSettings`              | [ResolutionSettings](#resolutionsettings) | TODO
`ApplyAnimationBlenderInTangerine`| bool                                      | Allows you to enable BlendAnimationEngine in Tangerine which is turned off by default
## Target

Default target names are `iOS`, `Android`, `Win`, `Mac`.

Property           | Type   | Description
-------------------|--------|------------
`CleanBeforeBuild` | bool   | if Clean step should be invoked before building the project
`Name`             | string | Target name will be diaplayed in Orange and Tangerine interface.
`Configuration`    | string | Configuration to build in terms of VS projects. Default targets have it set to `Release`
`Project`          | string | relative path to VS `.csproj` or `.sln` file
`BaseTarget`       | string | name of target to inherit.

If any property but `Name` and `BaseTarget` is not specified or set to null it will be inherited from specified `BaseTarget`.

## `XCodeProject`

Property     | Type | Description
-------------|------|------------
`DataFolder` |      | TODO
`Resources`  |      | TODO

## `AssetCache`:

Property         | Type   | Description
-----------------|--------|---------------------------------------
`ServerAddress`  | string | address of the FTP server
`ServerUsername` | string | username to use when connect to server
`ServerPath`     | string | TODO

## `PluginAssemblies`

Property             | Type           | Description
---------------------|----------------|-------------------------------------------------------------------------------
`OrangeAndTangerine` | List\<string\> | list of relative paths to assemblies to be loaded by both Tangerine and Orange
`Orange`             | List\<string\> | list of relative paths to assemblies to be loaded by Orange only
`Tangerine`          | List\<string\> | list of relative paths to assemblies to be loaded by Tangerine only

Assembly path may contain substitution tokens:
- `$(CONFIGURATION)` Either replaced by `Debug` or `Release`
- `$(HOST_APPLICATION)` replaced by either `Tangerine` or `Orange`

paths are relative to project directory

## `ResolutionSettings`

Property             | Type                              | Description
---------------------|-----------------------------------|-------------------------
`IsLandscapeDefault` | bool                              | default: true TODO
`Resolutions`        | List\<[Resolution](#resolution)\> | list of resolutions TODO
`Markers`            | List\<[Marker](#marker)\>         | list of markers TODO

### Resolution

Property            | Type           | Description
--------------------|----------------|----------------
`Name`              | string         | resolution name
`Width`             | number         | width
`Height`            | number         | height
`ResolutionMarkers` | List\<string\> | TODO

### Marker

Property          | Type   | Description
------------------|--------|------------
`Name`            | string | TODO
`LandscapeMarker` | string | TODO
`PortraitMarker`  | string | TODO

## `RemoteScripting`

Property              | Type           | Description
----------------------|----------------|------------
`ScriptsPath`         | string         | TODO
`ScriptsAssemblyName` | string         | TODO
`ReferencesPath`      | string         | TODO
`References`          | List\<string\> | TODO
`EntryPointsClass`    | string         | TODO
`RemoteStoragePath`   | string         | TODO