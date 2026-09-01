---
name: add-room-setting
description: 骷髅王项目新增或修改一个房间设置项（人数上限、轮数、限时、口令这类）要动的全部位置，含前后端 DTO、校验、两个设置对话框和说明气泡。当需要加房间配置、改设置项的取值范围、或设置改了不生效时使用。
---

# 新增房间设置项

一个设置项横跨七处。漏掉后端校验会让非法值进到规则引擎，漏掉前端类型会在 `npm run build` 时报错，漏掉说明气泡则用户无处可查。

## 后端

1. **契约** `src/SkullKing.Contracts/Rooms.cs` —— 三个地方都要加：`RoomSettingsDto`（下发给客户端）、`CreateRoomRequest`（建房）、`UpdateRoomSettingsRequest`（改设置，字段要可空，`null` 表示不改）。
2. **内存模型** `src/SkullKing.Application/Rooms/RoomSettings.cs` —— 加属性，并在 `Sanitized()` 里 clamp 到合法区间。所有入口都会过 `Sanitized()`，把校验写在这里就不会漏。
3. **建房** `RoomService.CreateRoomAsync` —— 从请求里取值。
4. **改设置** `RoomService.UpdateSettingsAsync` —— 加进 `room.Settings with { ... }`，并决定**对局进行中能不能改**：会牵动座位或轮数的一律锁住（加进那个 `room.Status == RoomStatus.Playing && (...)` 的判断里），只有像限时这种不影响牌局结构的才放行。
5. **下发** `RoomService.BuildStateFor` 里构造 `RoomSettingsDto` 的地方。

要持久化的话还要走一遍 skill `db-migration`。

## 前端

6. **类型** `client/src/api/types.ts` —— `RoomSettingsDto` 和两个 request 接口。这个文件是手写的，不会自动同步。
7. **两个对话框** `client/src/skins/im/RoomEntryDialog.vue`（新建群聊）和 `RoomSettingsDialog.vue`（群设置）。两处都要加，别只改一个。
8. **说明气泡** `client/src/skins/im/settingHints.ts` 加文案，然后在标签后面挂 `<FieldHint :text="settingHints.xxx" />`：

```html
<label class="field">
  <span>成员上限 <FieldHint :text="settingHints.maxPlayers" /></span>
  <select v-model.number="form.maxPlayers" class="input" :disabled="playing">
```

气泡按所在字段的宽度铺开（要求父级 `.field` / `.check` 有 `position: relative`），表单最后一项传 `up` 让它往上翻，否则会被弹窗内容区裁掉。

## 文案要求

说明文案要写清**实际逻辑**，不是重复字段名。用户看不到代码，这里是唯一的出处。比如「成员上限」要说明它不是开局门槛（2 人就能开）、坐满后进来的人转旁观；「议程轮数」要说明实际轮数还受 70 张牌的限制。

改了行为就回头核对 `settingHints.ts` 里的数字有没有过时。

## 验证

`dotnet test` + `npm --prefix client run build`。改了对局中能否修改的规则，补一个 `ResilienceTests` 里的用例（参考 `对局中不许改限时以外的设置`）。
