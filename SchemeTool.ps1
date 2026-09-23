$ErrorActionPreference = "Stop"

if ($PSVersionTable.PSVersion.Major -ne 5) {
    Write-Host "Powershell version $($PSVersionTable.PSVersion)' is not supported" -ForegroundColor Red
    return
}

if ([string]::IsNullOrEmpty($GarbroRoot) -or (-not(Test-Path -Path "$GarbroRoot" -PathType Container))) {
    Write-Host "Specified Garbro root path '${GarbroRoot}' is not valid" -ForegroundColor Red
    return
}

$GarbroRoot = Resolve-Path -Path "$GarbroRoot"

Add-Type -Path (Join-Path -Path "$GarbroRoot" -ChildPath "ArcFormats.dll")
Add-Type -Path (Join-Path -Path "$GarbroRoot" -ChildPath "ArcLegacy.dll")
Add-Type -Path (Join-Path -Path "$GarbroRoot" -ChildPath "GameRes.dll")
Add-Type -Path (Join-Path -Path "$GarbroRoot" -ChildPath "Newtonsoft.Json.dll")

$GarbroAssemblies = @{}

[System.AppDomain]::CurrentDomain.GetAssemblies() | Where-Object -Property Location -Match "^$([Regex]::Escape($GarbroRoot))" | ForEach-Object {
    $GarbroAssemblies[($_.FullName -split ',')[0].Trim()] = $_
}

$AssemblyResolver = [System.ResolveEventHandler] {
    param($s, $e)
    return $GarbroAssemblies[(($e.Name -split ',')[0].Trim())]
}

[System.AppDomain]::CurrentDomain.add_AssemblyResolve($AssemblyResolver)

function Get-GarbroGameDatabase {
    param(
        [string]$DBPath
    )
    if (-not(Test-Path -Path "$DBPath" -PathType Leaf)) {
        throw "Specified Garbro game scheme database file '${DBPath}' is not valid"
    }
    $DBPath = Resolve-Path -Path "$DBPath"
    try {
        $DB = [System.IO.FileStream]::new($DBPath, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read);
        try {
            $null = [GameRes.FormatCatalog]::Instance.GetSerializedSchemeVersion($DB)
            $zstream = [GameRes.Compression.ZLibStream]::new($DB, [GameRes.Compression.CompressionMode]::Decompress, $True);
            try {
                return [System.Runtime.Serialization.Formatters.Binary.BinaryFormatter]::new().Deserialize($zstream) -as [GameRes.SchemeDatabase]
            }
            catch {
                Write-Host "Failed to deserialize Garbro SchemeDatabase: $($_.Exception.Message)" -ForegroundColor Red
                throw
            }
            finally {
                if ($null -ne $zstream) {
                    $zstream.Close()
                }
            }
        }
        catch {
            Write-Host "Failed to parse Garbro game scheme database: $($_.Exception.Message)" -ForegroundColor Red
            throw
        }
        finally {
            if ($null -ne $DB) {
                $DB.Close()
            }
        }
    }
    catch {
        Write-Host "Failed to open Garbro game scheme database: $($_.Exception.Message)" -ForegroundColor Red
        throw
    }
}

function Save-GarbroGameDatabase {
    param(
        [GameRes.SchemeDatabase]$Database,
        [string]$OutputPath
    )
    if ($null -eq $Database) {
        throw "Specified Garbro game scheme database object is null"
    }
    if (-not(Test-Path -IsValid -Path "$OutputPath")) {
        throw "Specified output path '${OutputPath}' is not valid"
    }
    $OutputPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath("$OutputPath")
    $Output = [System.IO.FileStream]::new($OutputPath, [System.IO.FileMode]::OpenOrCreate, [System.IO.FileAccess]::ReadWrite);
    try {
        [GameRes.FormatCatalog]::Instance.SerializeScheme($Output, $Database);
    }
    catch {
        Write-Host "Failed to serialize Garbro game SchemeDatabase: $($_.Exception.Message)" -ForegroundColor Red
        throw
    }
    finally {
        $Output.Close()
    }
}

function Dump-GarbroGameDatabaseToJson {
    param(
        [GameRes.SchemeDatabase]$Database,
        [string]$OutputPath
    )
    if ($null -eq $Database) {
        throw "Specified Garbro game scheme database object is null"
    }
    if (-not(Test-Path -IsValid -Path "$OutputPath")) {
        throw "Specified output path '${OutputPath}' is not valid"
    }
    $OutputPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath("$OutputPath")
    $SerializationSetting = [Newtonsoft.Json.JsonSerializerSettings]::new()
    $SerializationSetting.ContractResolver = [System.Activator]::CreateInstance($GarbroAssemblies['GameRes'].GetType('GameRes.FormatCatalog+IncludeFieldsContractResolver'))
    if ($null -eq $SerializationSetting.Converters) {
        $SerializationSetting.Converters = [System.Collections.Generic.List[Newtonsoft.Json.JsonConverter]]::new()
    }
    $SerializationSetting.Converters.Add([System.Activator]::CreateInstance($GarbroAssemblies['GameRes'].GetType('GameRes.FormatCatalog+ByteArrayToHexStringConverter')))
    $SerializationSetting.Formatting = [Newtonsoft.Json.Formatting]::Indented
    [Newtonsoft.Json.JsonConvert]::SerializeObject($Database, $SerializationSetting) | Out-File -FilePath "$OutputPath" -Encoding utf8
}
