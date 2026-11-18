param([string]$CanjuDir = (Join-Path $PSScriptRoot '../research/world-boss/canju-release/com.yx.canju'))
$ErrorActionPreference = 'Stop'
$canju = (Resolve-Path -LiteralPath $CanjuDir).Path
$canjuManifest = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $canju 'manifest.json') | ConvertFrom-Json
if ($canjuManifest.id -ne 'com.yx.canju' -or $canjuManifest.version -ne '0.2.23') {
    throw '需要残局求解 0.2.23 的解压目录'
}
$pinned = @{
    'plugins/Canju.dll' = '9C0E0C605B14F3CE1E813D717D8B06D724B9F5E6C2AE20B7DEEA2BCC7583988F'
    'native/YxBattleSim.dll' = '8C90B42C2AE1CA060FEB38A7242EF6A574ADD4E9CF2E4501E227F6432BFC93D7'
    'data/battle_config.json' = '638D13DC6A14E49B9F06B588303EB5E305C912E8FC1F2249163A727B2B18BF27'
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
