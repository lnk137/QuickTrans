# Agent 约束

## 项目工作流程

- 保留现有 Git 仓库及提交历史。
- 围绕用户请求进行修改，不覆盖无关改动。
- 行为、配置或构建流程变化时，及时检查并更新文档。
- 及时清理任务产生的临时文件，不将构建产物、安装包、凭据、API Key 或本地配置提交到 Git。
- 执行与改动相适应的检查，并说明未能执行的检查。
- 需要使用 Python 时，使用 Conda `base` 环境。

## Commit 消息规范

仅要求 **commit 消息全部使用英文**，包括标题、可选正文和尾注；此要求不限制文档、代码注释或日常沟通的语言。

标题以对应类型的实际 Emoji 字符开头。下表中的 shortcode 仅供参考，提交时优先使用实际 Emoji。

格式如下：

```text
<emoji> <type>[(optional-scope)]: <imperative summary>

<optional body explaining why the change is needed>

<optional footers, such as issue references or breaking-change details>
```

- 类型使用小写；可选范围应简短，例如 `macos` 或 `windows`。
- 摘要必须具体且非空，使用英文祈使句，末尾不加句号。
- 标题尽量控制在 72 个字符以内。
- 标题、正文和尾注之间空一行。
- 每次提交聚焦一个逻辑改动，提交前检查暂存区 diff。
- 根据改动的主要目的选择类型。
- 破坏性变更在冒号前加 `!`，并通过 `BREAKING CHANGE:` 尾注说明。
- 不为追溯应用此规范而重写已发布的历史。

| 类型 | Emoji | Shortcode | 说明 |
| --- | --- | --- | --- |
| `feat` | ✨ | `:sparkles:` | 新功能、新特性 |
| `fix` | 🐛 | `:bug:` | 修复 bug |
| `docs` | 📚 或 📝 | `:books:` 或 `:memo:` | 文档修改 |
| `style` | 🎨 | `:art:` | 不影响功能的代码格式调整 |
| `refactor` | 🔨 或 ♻️ | `:hammer:` 或 `:recycle:` | 不改变功能的代码重构 |
| `perf` | ⚡ | `:zap:` | 性能优化 |
| `test` | ✅ | `:white_check_mark:` | 添加或修改测试 |
| `chore` | 🔧 | `:wrench:` | 构建工具、脚本或配置维护 |
| `ci` | 👷 | `:construction_worker:` | CI/CD 相关变更 |
| `build` | 🏗️ | `:building_construction:` | 构建系统或外部依赖配置变更 |
| `revert` | ⏪ | `:rewind:` | 回滚提交 |
| `security` | 🔒 | `:lock:` | 安全性改进 |
| `release` | 🚀 | `:rocket:` | 版本发布 |
| `init` | 🎉 | `:tada:` | 项目初始化 |
| `delete` | 🔥 | `:fire:` | 移除代码或文件 |
| `upgrade` | ⬆️ 或 ⬇️ | `:arrow_up:` 或 `:arrow_down:` | 依赖升级或降级 |

英文提交示例：

```text
✨ feat(macos): add menu bar translation
🐛 fix(macos): keep settings hidden when launched at login
📚 docs: document macOS installation steps
🎨 style: normalize Swift indentation
♻️ refactor: simplify translation request handling
⚡ perf: cache repeated translation results
✅ test: cover login launch behavior
🔧 chore: ignore generated macOS artifacts
👷 ci: run tests on pull requests
🏗️ build(macos): sign and verify the complete app bundle
⏪ revert: restore the previous hotkey behavior
🔒 security: redact API keys from logs
🚀 release: publish version 0.1.9
🎉 init: initialize QuickTrans
🔥 delete: remove unused debug helpers
⬆️ upgrade: update dependency versions
⬇️ upgrade: downgrade an incompatible dependency
```

仓库提供 `.gitmessage` 作为编辑器中的提交提示。克隆后执行：

```bash
git config --local commit.template .gitmessage
```

模板仅用于提醒，不是强制校验钩子。通过 `git commit -m` 传入的消息同样必须遵守上述规范。
