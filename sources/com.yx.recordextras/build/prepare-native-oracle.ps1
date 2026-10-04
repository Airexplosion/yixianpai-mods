param([string]$GameSource = (Join-Path $PSScriptRoot '../../downloads/recordextras-game/BattleCharacterUI.cs'))
$ErrorActionPreference = 'Stop'
$gameText = Get-Content -LiteralPath $GameSource -Raw
# Extract only the two original methods for differential testing. This file is
# generated locally from the user's game, ignored, and never packaged.
$start = $gameText.IndexOf('private int CalExtraSpeed(')
$end = $gameText.IndexOf('private KeywordDetailData TryGetKeywordData(', $start)
if ($start -lt 0 -or $end -lt 0) { throw 'Native speed method boundaries not found' }
$methods = $gameText.Substring($start, $end - $start)
$methods = $methods.Replace('private int CalExtraSpeed(', 'public static int CalExtraSpeed(').Replace('private bool IsSameWuxing(', 'private static bool IsSameWuxing(')
$oracleSource = "using System.Collections.Generic;`nusing Proto;`ninternal static class NativeSpeedOracle`n{`n" + $methods + "`n}`n"
Set-Content -LiteralPath (Join-Path $PSScriptRoot 'NativeSpeedOracle.cs') -Value $oracleSource -Encoding utf8
