param([string]$CanjuDir = (Join-Path $PSScriptRoot '../research/world-boss/canju-0.2.24/com.yx.canju'))
$ErrorActionPreference = 'Stop'
$canju = (Resolve-Path -LiteralPath $CanjuDir).Path
$canjuManifest = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $canju 'manifest.json') | ConvertFrom-Json
if ($canjuManifest.id -ne 'com.yx.canju' -or $canjuManifest.version -ne '0.2.24') {
    throw '需要残局求解 0.2.24 的解压目录'
}
$pinned = @{
    'plugins/Canju.dll' = 'BCC4D1BE3DB9F25A9D152FA1EC1C636187A08D1B0E1DB98EFD0566264F157B89'
    'native/YxBattleSim.dll' = '3A0858365269741BD054263FE1F95B84CFC36ADCF0D1758FC46356694F820584'
    'data/battle_config.json' = 'E99A55853CFBCC4A6FE99D81696FD2349F14F6852126C7DAB43FCBDD423ECC0B'
    'data/card_effects.json' = '5DC6BB67638A81F4A857F84B386A993B60407CF824A33D61175A759E8980AF9C'
}
foreach ($file in $pinned.Keys) {
    if ((Get-FileHash -LiteralPath (Join-Path $canju $file) -Algorithm SHA256).Hash -ne $pinned[$file]) {
        throw "依赖文件版本不符 $file"
    }
}
$project = Join-Path $PSScriptRoot 'MistakeBook.csproj'
$tests = Join-Path $PSScriptRoot 'tests/MistakeBook.Tests.csproj'
$packer = Join-Path $PSScriptRoot '../yxp-mod-loader/tools/yx-patch/yx-patch.csproj'
dotnet build $project -c Release "-p:CanjuDir=$canju" --nologo
if ($LASTEXITCODE -ne 0) { throw 'Mod 编译失败' }
dotnet test $tests -c Release "-p:CanjuDir=$canju" --nologo
if ($LASTEXITCODE -ne 0) { throw '错题本测试失败' }
$stage = Join-Path $PSScriptRoot ('obj/package/' + [guid]::NewGuid().ToString('N') + '/com.yx.mistakebook')
foreach ($dir in 'native','data','plugins') {
    New-Item -ItemType Directory -Path (Join-Path $stage $dir) -Force | Out-Null
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'manifest.json') -Destination $stage
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'bin/Release/net40/MistakeBook.dll') -Destination (Join-Path $stage 'plugins')
foreach ($file in 'native/YxBattleSim.dll','data/battle_config.json','data/card_effects.json') {
    Copy-Item -LiteralPath (Join-Path $canju $file) -Destination (Join-Path $stage $file)
}
dotnet run --project $packer -c Release -- pack $stage --out (Join-Path $PSScriptRoot 'dist')
if ($LASTEXITCODE -ne 0) { throw '安装包校验或打包失败' }
