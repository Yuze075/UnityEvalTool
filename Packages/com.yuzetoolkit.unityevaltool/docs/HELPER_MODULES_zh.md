# Helper 参考

[README](../README_zh.md) | [English](HELPER_MODULES.md) | [Runtime 服务](RUNTIME_SERVICES_zh.md) | [项目设计](PROJECT_DESIGN_zh.md) | [高级说明](ADVANCED_USAGE_zh.md)

[![Runtime](https://img.shields.io/badge/Runtime-6%20modules-2ecc71)](#runtime-helpers)
[![Editor](https://img.shields.io/badge/Editor-9%20modules-3498db)](#editor-helpers)
[![Tool](https://img.shields.io/badge/Broker%20MCP-3%20tools-orange)](../README_zh.md#mcp-固定流程)

完成 `unity_status` 和 `unity_connect` 后，Broker 的 `eval` 会在选中的 Unity 内运行。Agent 在这个 eval 中从 `tools://` 和 `tools://<Tool/Path>` import helper module。内置 module 从带 `[EvalTool(name, description)]` 的 partial C# class 生成；source generator 会为它们生成 `IEvalTool` 元数据。每个 C# module 导出语义函数，每次调用都会确认 tool 仍处于启用状态，再通过 PuerTS 调用 C# public 实例方法，并把返回值交给 Unity 侧 executor 格式化。项目和其他包的 JavaScript 扩展需要通过 `tools://UnityEval` 显式加载。

生成的 C# helper 应优先返回基础类型、`List<T>`、`Dictionary<string, TValue>` 或由这些类型组成的数据。服务端会把这类结果作为 JSON text content 返回，这是最稳定、最推荐的工具返回形态。

常见流程优先使用 helper module，因为它们的说明更集中、返回数据更稳定。helper 未覆盖时，可以在 `eval` 中通过 PuerTS `CS.*` 直接调用 Unity/C# API；反复使用的项目专用逻辑应沉淀成 C# tool 或显式加载的 JavaScript helper。

Discovery 起点：

```javascript
async function execute() {
  const index = await import('tools://');
  return index.description;
}
```

## 模块索引

| 分类 | 模块 |
|---|---|
| Runtime helpers | `tools://Runtime`, `tools://Runtime/Objects`, `tools://Runtime/Components`, `tools://Runtime/Diagnostics`, `tools://Runtime/Reflection`, `tools://Runtime/Inspect` |
| Editor helpers | `tools://Editor`, `tools://Editor/Assets`, `tools://Editor/Importers`, `tools://Editor/Scenes`, `tools://Editor/Prefabs`, `tools://Editor/Serialized`, `tools://Editor/Project`, `tools://Editor/Pipeline`, `tools://Editor/Validation` |

Runtime helper 可在 Editor 或 Runtime/Player 中运行，前提是底层 Unity API 可用。Editor helper 依赖 `UnityEditor`，在 Runtime/Player 中会明确失败。

生成 helper 函数使用位置参数，例如 `const assets = await import('tools://Editor/Assets'); assets.find('t:Prefab', 20, ['Assets'])`。生成的 C# module 会暴露 `functions[].description`、有序的 `functions[].parameters`、显式声明的 safety flags、`conditionalRequiresConfirmation` 等条件安全提示，也会导出 `isEnabled()` 用于读取当前启用状态。

## Runtime Helpers

### `tools://Runtime`

环境状态和 Unity 日志。

| 函数 | 用途 |
|---|---|
| `getState()` | 环境、Unity 版本、平台、播放状态、路径、active scene、已注册 tools。 |
| `getRecentLogs(count?, type?)` | MCP 捕获的 Unity 日志。 |
| `clearLogs()` | 清空 MCP log buffer。 |

### `tools://Runtime/Objects`

Scene GameObject、hierarchy 和 Transform 操作。

| 函数 | 用途 | 安全 |
|---|---|---|
| `find(name, limit?)` | 按精确 `name` 查找 active GameObject，返回轻量 selector。 | 只读 |
| `findOne(name)` | 按精确 `name` 查找第一个 active GameObject，返回轻量 selector。 | 只读 |
| `findByPath(path, includeInactive?)` | 按精确 hierarchy path 查找一个 GameObject。 | 只读 |
| `findByTag(tag, limit?)` | 使用 Unity tag 查询 active GameObject。 | 只读 |
| `get(target)` | 检查单个 GameObject。 | 只读 |
| `create(name?, primitive?, parent?, localPosition?, position?, localScale?)` | 创建空对象或 primitive GameObject。 | 修改场景 |
| `destroy(target, confirm?)` | 销毁 GameObject。 | 需要 `confirm: true` |
| `duplicate(target, name?)` | 复制 GameObject。 | 修改场景 |
| `setParent(target, parent?, worldPositionStays?)` | 修改 hierarchy 父对象。 | 修改场景 |
| `setTransform(target, position?, localPosition?, rotationEuler?, localRotationEuler?, localScale?)` | 设置位置、旋转或缩放。 | 修改场景 |
| `setActive(target, active)` | 修改 active 状态。 | 修改场景 |
| `setNameLayerTag(target, name?, layer?, tag?)` | 修改 name、layer 或 tag。 | 修改场景 |

### `tools://Runtime/Components`

Component 读取、编辑和实例方法调用。

| 函数 | 用途 | 安全 |
|---|---|---|
| `list(target)` | 列出 GameObject 上的 Component。 | 只读 |
| `get(target, type?, index?, includeValues?)` | 按 type/index 读取一个 Component。默认只返回成员定义；传 `includeValues = true` 才调用 public getter。 | 只读 |
| `find(typeName, limit?, includeInactive?)` | 按 C# 类型名查找 live Component，返回组件摘要和 GameObject selector。 | 只读 |
| `add(target, type)` | 添加 Component。 | 修改场景 |
| `remove(target, type?, index?, confirm?)` | 删除 Component。 | 需要 `confirm: true` |
| `setProperty(target, type, member, value, index?, includeNonPublic?, includeStatic?, confirmDangerous?)` | 设置一个 field/property。 | 修改 Component |
| `setProperties(target, type, values, index?, includeNonPublic?, includeStatic?, confirmDangerous?)` | 设置多个 fields/properties。 | 修改 Component |
| `callMethod(target, type, method, args?, index?, includeNonPublic?, confirmDangerous?)` | 调用 instance method。 | 取决于方法 |
| `listTypes(query?, limit?)` | 搜索可用 Component 类型。 | 只读 |

`Runtime/Objects.find` 不再接受 `path`、`tag`、`component` 或 `includeInactive` selector 模式。需要使用 `findByPath`、`findByTag` 或 `Runtime/Components.find`，让每种查询都有明确成本模型。`Runtime/Components.get` 默认不读取所有 public property value，因为 Unity Component getter 可能很重；只有确实需要值时才显式请求。非 public method 调用需要 `includeNonPublic: true` 和 `confirmDangerous: true`。

### `tools://Runtime/Diagnostics`

只读运行时诊断。

| 函数 | 用途 |
|---|---|
| `listCameras()` | Scene cameras 和常用设置。 |
| `getPhysicsState()` | Physics2D/3D 设置以及 Collider、Rigidbody 摘要。 |
| `getGraphicsState()` | Render pipeline、quality、color space。 |
| `listCanvases()` | Canvas 对象、render settings 和 EventSystem。 |
| `listLoadedTextures(limit?)` | 已加载 Texture 对象的尺寸和类型。 |

### `tools://Runtime/Inspect`

C#/Unity 对象引用格式化辅助。

| 函数 | 用途 |
|---|---|
| `describe(value?, depth?)` | 返回默认摘要 DTO。 |
| `format(value?, mode?, depth?)` | 用 `default`、`summary`、`name`、`path`、`text`、`json`、`yaml` 格式化值。 |
| `toName(value?)` | 返回 Unity/C# 对象名称。 |
| `toPath(value?)` | 返回场景层级路径或资产路径。 |
| `toJson(value?, mode?, depth?)` | 返回格式化值的 JSON 字符串。 |
| `toYaml(value?, depth?)` | 返回格式化值的 YAML 字符串。 |

### `tools://Runtime/Reflection`

项目自定义 API 的 C# 类型发现和 static method 调用。

| 函数 | 用途 | 安全 |
|---|---|---|
| `getNamespaces()` | 列出 public namespaces。 | 只读 |
| `getTypes(namespaceName)` | 列出某 namespace 下的 public types。 | 只读 |
| `getTypeDetails(fullName)` | 列出某 type 的 public members。 | 只读 |
| `findMethods(query?, type?, includeNonPublic?, confirmDangerous?, limit?)` | 搜索 public methods。 | 非 public 搜索需要 `confirmDangerous: true` |
| `callStaticMethod(type, method, args?, includeNonPublic?, confirmDangerous?)` | 调用 static method。 | 非 public 调用需要 `confirmDangerous: true` |

## Editor Helpers

### `tools://Editor`

Editor 状态、编译、Selection、菜单、播放模式和截图。

| 函数 | 用途 | 安全 |
|---|---|---|
| `getState()` | Editor 状态、active scene、selection 摘要。 | 只读 |
| `getCompilationState()` | Unity 侧诊断 request 状态及错误/警告计数；等待编译使用 Broker `unity_status`。 | 只读 |
| `requestScriptCompilation()` | 只请求一次脚本编译，随后结束 eval 并通过 Broker 状态等待；必要时先退出 PlayMode。 | 可能触发 reload |
| `scheduleAssetRefresh()` | 只请求一次脚本安全 AssetDatabase refresh，随后结束 eval 并通过 Broker 状态等待；必要时先退出 PlayMode。 | 可能触发 reload |
| `getCompilerMessages(count?)` | 最近类似编译器的错误/警告。 | 只读 |
| `getSelection()` / `setSelection(items)` | 读取或设置 Editor selection。 | 修改 selection |
| `executeMenuItem(path, confirm?)` | 执行 Editor menu item。 | 非 UnityEvalTool 菜单需要 `confirm: true` |
| `setPlayMode(isPlaying)` / `setPause(isPaused)` | 控制播放/暂停状态。 | 改变 Editor 状态 |
| `screenshotGameView(path?)` | 捕获 Game View。 | 写入截图文件 |

### `tools://Editor/Assets`

AssetDatabase 搜索、项目文本 IO、依赖、脚本、材质和脚本安全刷新。

| 函数 | 用途 | 安全 |
|---|---|---|
| `find(filter, limit?, folders?)` / `findPaths(filter, limit?, folders?)` / `findNames(filter, limit?, folders?)` | 使用 Unity filter 搜索资产，分别返回摘要、路径或名称。 | 只读 |
| `getInfo(path)` | 资产元数据。 | 只读 |
| `readText(path)` / `writeText(path, text, refresh?, confirmOverwrite?)` | 读写文本资源。 | 写入会修改项目 |
| `createFolder(name, parent?)` | 创建 AssetDatabase 文件夹。`parent` 默认是 `Assets`。 | 修改项目 |
| `copy(from, to, confirmOverwrite?)` | 复制资产。 | 修改项目 |
| `move(from, to, confirm?)` | 移动或重命名资产。 | 需要 `confirm: true` |
| `deleteAsset(path, confirm?)` | 删除资产。 | 需要 `confirm: true` |
| `refreshNow()` | 请求脚本安全 AssetDatabase refresh 并返回编译/导入状态；必要时先退出 PlayMode。 | 可能触发 reload |
| `getDependencies(path, recursive?)` | 资产依赖。 | 只读 |
| `findReferences(path, folders, limit?)` | 在明确 folders 范围内查找资产引用。省略 limit 时最多返回 100 条。 | 只读 |
| `createScript(path, className?, namespaceName?, confirmOverwrite?)` | 创建 MonoBehaviour 脚本。 | 可能触发 reload |
| `applyScriptTextEdits(path, edits, refresh?, confirm?)` | patch 脚本文件。 | 可能触发 reload |
| `createMaterial(path, shaderName?, properties?, confirmOverwrite?)` | 创建 Material 资产。 | 修改项目 |

### `tools://Editor/Importers`

AssetImporter 检查和编辑。

| 函数 | 用途 | 安全 |
|---|---|---|
| `get(path, includeProperties?, propertyLimit?)` | Importer 摘要和可选 serialized properties。属性枚举会被限制数量。 | 只读 |
| `setProperty(path, propertyPath, value, saveAndReimport?, confirm?)` | 设置一个 importer SerializedProperty。 | 修改 importer |
| `setMany(path, changes, saveAndReimport?, confirm?)` | 设置多个 importer properties；`changes` 可为 `{ propertyPath: value }` map 或 `{ propertyPath, value }[]`。 | 修改 importer |
| `reimport(path, confirm?)` | 强制重新导入。 | 需要 `confirm: true` |

### `tools://Editor/Scenes`

Scene 文件和已打开 Scene hierarchy。

| 函数 | 用途 | 安全 |
|---|---|---|
| `listOpenScenes()` | 列出已打开 Scene。 | 只读 |
| `getSceneHierarchy(depth?, includeComponents?, limit?)` | 读取已打开 Scene hierarchy。 | 只读 |
| `openScene(path, mode?, confirm?, saveDirtyScenes?)` | 打开 Scene。 | 改变 Editor 状态 |
| `createScene(path?, setup?, mode?, confirm?, saveDirtyScenes?)` | 创建新 Scene。 | 修改项目/session |
| `saveScene()` / `saveSceneAs(path)` | 保存 Scene。 | 写入 Scene asset |
| `setActiveScene(path)` | 设置 active scene。 | 改变 Editor 状态 |

### `tools://Editor/Prefabs`

Prefab instance、asset、stage、override 和 unpack 操作。

| 函数 | 用途 | 安全 |
|---|---|---|
| `instantiate(path, parent?, position?)` | 实例化 Prefab asset。 | 修改场景 |
| `createFromObject(target, path, confirmOverwrite?)` | 把 scene object 保存为 Prefab。 | 修改项目 |
| `createVariant(basePath, path, confirmOverwrite?)` | 创建 Prefab variant。 | 修改项目 |
| `openStage(path)` / `closeStage()` / `saveStage()` | Prefab Stage 工作流。 | 改变 Editor/project 状态 |
| `getOverrides(target)` | 读取 Prefab overrides。 | 只读 |
| `applyOverrides(target, confirm?)` | Apply overrides。 | 需要 `confirm: true` |
| `revertOverrides(target, confirm?)` | Revert overrides。 | 需要 `confirm: true` |
| `unpack(target, mode?, confirm?)` | Unpack Prefab instance。 | 需要 `confirm: true` |

### `tools://Editor/Serialized`

SerializedObject 和 Inspector property 读写。

| 函数 | 用途 | 安全 |
|---|---|---|
| `get(target, propertyPath?, limit?)` | 读取一个 `propertyPath`；不传路径时最多读取 `limit` 个可见 property。 | 只读 |
| `set(target, propertyPath, value, confirm?)` | 设置一个 SerializedProperty value。 | 修改对象/资产 |
| `setMany(target, changes, confirm?)` | 设置多个 SerializedProperty value；`changes` 可为 `{ propertyPath: value }` map 或 `{ propertyPath, value }[]`。 | 修改对象/资产 |
| `resizeArray(target, propertyPath, size, confirm?)` | 调整 serialized array 大小。 | 修改对象/资产 |
| `insertArrayElement(target, propertyPath, index?, confirm?)` | 插入数组元素。 | 修改对象/资产 |
| `deleteArrayElement(target, propertyPath, index, confirm?)` | 删除数组元素。 | 修改对象/资产 |

target 可以是 `assetPath`、`guid`、`instanceId` 或 GameObject selector。
支持写入的常见类型包括 integer、boolean、float、string、color、object reference、enum、Vector2/3/4、Quaternion、Vector2Int/3Int、Rect/RectInt、Bounds/BoundsInt 和 AnimationCurve。不支持的 property 类型会明确报错。

### `tools://Editor/Project`

项目级诊断。

| 函数 | 用途 |
|---|---|
| `getProjectSettings()` | Product/company/application id、tags、layers。 |
| `getProfilerState()` | Profiler 可用性和 recording flags。 |
| `getToolState()` | 当前 Editor tool、pivot mode、pivot rotation。 |

### `tools://Editor/Pipeline`

Package Manager、Test Runner 和 BuildPipeline 工作流。

| 函数 | 用途 | 安全 |
|---|---|---|
| `listPackages()` | 列出 registered packages。 | 只读 |
| `addPackage(packageId, confirm?)` | 添加 package。 | 需要 `confirm: true` |
| `removePackage(packageName, confirm?)` | 移除 package。 | 需要 `confirm: true` |
| `searchPackages(packageName?)` | 搜索 package registry。 | 只读/request |
| `getPackageRequest(id)` | 查询 package request。 | 只读 |
| `runTests(mode?, testName?)` / `getTestRun(id)` | 运行或查询测试。 | 可选 Test Framework；未安装时不支持 |
| `getBuildSettings()` | 读取 build scenes 和 target。 | 只读 |
| `buildPlayer(locationPathName, confirm?)` | 构建 player。 | 需要 `confirm: true` |
| `getBuild(id)` | 查询 build request。 | 只读 |

### `tools://Editor/Validation`

项目健康检查。

| 函数 | 用途 |
|---|---|
| `run(folders?, limit?, includeProjectWideChecks?)` | 默认只运行 loaded-scene 缺失引用检查；项目级检查需要显式开启。 |
| `missingScripts(folders?, limit?)` | 查找缺失 MonoBehaviour scripts。Prefab 扫描只在明确 folders 范围内运行。 |
| `missingReferences(limit?)` | 查找 loaded scenes 中损坏的 serialized references。 |
| `serializedFieldTooltips(folders, limit?)` | 在明确 folders 范围内检查序列化字段是否有 `[Tooltip]`。 |

## 常见工作流

| 目标 | 优先入口 |
|---|---|
| 检查 Unity 环境 | `tools://Runtime#getState()` |
| 检查场景对象 | `tools://Runtime/Objects` |
| 读取 live component 数据 | `tools://Runtime/Components` |
| 编辑 Inspector 字段 | `tools://Editor/Serialized` |
| 搜索项目资源 | `tools://Editor/Assets#find()` |
| 修改 importer 设置 | `tools://Editor/Importers` |
| 处理 Prefab | `tools://Editor/Prefabs` |
| 运行测试或构建 | `tools://Editor/Pipeline` |
| 调用自定义 static C# API | `tools://Runtime/Reflection` |
| 执行一次性 Unity/C# 代码 | `eval` 中的 PuerTS `CS.*` interop |
| 使用底层 commands | [高级说明](ADVANCED_USAGE_zh.md) |
