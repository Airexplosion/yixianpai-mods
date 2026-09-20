# 弈仙牌 mod 清单

弈仙牌 MOD 管理器「浏览」页读的在线清单(Dalamud 式)。管理器按 `mods.json` 列出可安装的 mod、一键安装 / 更新;所有联网都带 GitHub 代理镜像兜底。

- 内置默认清单地址就是本仓库的 `mods.json`(raw)。
- 用户也可以在管理器「设置 → 在线仓库」里加别的清单地址。

## `mods.json` 格式

```json
{
  "schema": 1,
  "mods": [
    {
      "id": "com.example.mymod",
      "name": "我的 Mod",
      "version": "1.0.0",
      "author": "你的名字",
      "description": "一句话说明",
      "sdkVersion": ">=1.0.0 <2.0.0",
      "gameVersion": ">=1.7.0",
      "repo": "https://github.com/你的用户名/你的mod仓库",
      "download": "https://github.com/你的用户名/你的mod仓库/releases/download/v1.0.0/com.example.mymod-1.0.0.zip",
      "sha256": "可选:zip 的 sha256,填了管理器会校验"
    }
  ]
}
```

| 字段 | 必填 | 说明 |
|---|---|---|
| `id` | 是 | mod 的反向域名 id,和 manifest 里一致 |
| `name` / `version` / `author` | 是 | 显示名 / semver 版本 / 作者 |
| `description` | 否 | 一句话说明 |
| `sdkVersion` / `gameVersion` | 否 | 兼容范围(展示用) |
| `repo` | 否 | 给人看的仓库 / 主页(https) |
| `download` | 是 | 打包 zip 的下载直链(https),通常是 GitHub Release 资产,即 `yx-patch pack` 出的 `<id>-<version>.zip` |
| `sha256` | 否 | zip 的 sha256,填了就做完整性校验(防代理篡改 / 损坏) |

## 怎么把你的 mod 收进清单

1. 用 [yixianpai-mod-sdk](https://github.com/Airexplosion/yixianpai-mod-sdk) 开发好你的 mod。
2. `yx-patch pack 你的目录` 打出 `dist/<id>-<version>.zip`。
3. 在你自己的 mod 仓库发一个 **Release**,把那个 zip 作为资产上传。
4. 给本仓库发 PR,在 `mods.json` 的 `mods` 数组里加一条,`download` 指向那个 Release 资产直链。
5. 版本更新:发新 Release + 改 `mods.json` 里的 `version` 和 `download`。

> `download` 建议指向 GitHub Release 资产而不是仓库里的文件——Release 是稳定直链,也便于代理镜像加速。
