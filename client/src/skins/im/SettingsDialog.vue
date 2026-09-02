<script setup lang="ts">
import { ref } from 'vue'

import ModalShell from './ModalShell.vue'
import { useSessionStore } from '@/stores/session'
import { useStealthStore } from '@/stores/stealth'
import {
  getThemePreference,
  setThemePreference,
  THEME_OPTIONS,
  type ThemePreference,
} from '@/theme'

const emit = defineEmits<{ close: [] }>()

const session = useSessionStore()
const stealth = useStealthStore()

const nickname = ref(session.nickname)
const capturing = ref(false)
const saving = ref(false)
const theme = ref(getThemePreference())

function pickTheme(next: ThemePreference) {
  setThemePreference(next)
  theme.value = next
}

async function saveNickname() {
  const next = nickname.value.trim()

  if (!next || next === session.nickname) {
    return
  }

  saving.value = true

  try {
    await session.rename(next)
  } finally {
    saving.value = false
  }
}

/** 关窗前把改了一半的名字落下去，不然用户会以为改完了。 */
async function done() {
  await saveNickname()
  emit('close')
}

function captureKey(event: KeyboardEvent) {
  if (!capturing.value) {
    return
  }

  event.preventDefault()
  event.stopPropagation()

  if (event.key === 'Tab') {
    return
  }

  stealth.settings.bossKey = event.key
  capturing.value = false
}

async function onNotifyToggle(on: boolean) {
  await stealth.setDesktopNotify(on)
}
</script>

<template>
  <ModalShell title="设置" :width="440" @close="done">
    <section class="group">
      <h3>个人</h3>

      <label class="field">
        <span>显示名称</span>
        <div class="inline">
          <input v-model="nickname" class="input" maxlength="20" />
          <button class="btn" :disabled="saving" @click="saveNickname">保存</button>
        </div>
      </label>
    </section>

    <section class="group">
      <h3>外观</h3>

      <div class="field">
        <span>主题</span>
        <div class="theme-picks" role="radiogroup" aria-label="主题">
          <button
            v-for="option in THEME_OPTIONS"
            :key="option.id"
            type="button"
            class="theme-pick"
            role="radio"
            :aria-checked="theme === option.id"
            :class="{ on: theme === option.id }"
            @click="pickTheme(option.id)"
          >
            {{ option.label }}
          </button>
        </div>
        <p class="muted note">跟随系统时随手机深浅色一起变。</p>
      </div>
    </section>

    <section class="group">
      <h3>隐私</h3>

      <label class="field">
        <span>窗口标题</span>
        <input v-model="stealth.settings.documentTitle" class="input" maxlength="30" />
      </label>

      <label class="field">
        <span>快速切换按键</span>
        <div class="inline">
          <button class="btn key" :class="{ on: capturing }" @keydown="captureKey" @click="capturing = true">
            {{ capturing ? '请按一个键…' : stealth.bossKeyLabel }}
          </button>
          <span class="muted note">再按一次切回</span>
        </div>
      </label>

      <label class="check">
        <input v-model="stealth.settings.hideOnBlur" type="checkbox" />
        <span>切走窗口一段时间后自动切换</span>
      </label>

      <label v-if="stealth.settings.hideOnBlur" class="field">
        <span>自动切换延迟</span>
        <select v-model.number="stealth.settings.blurDelaySeconds" class="input">
          <option :value="3">3 秒</option>
          <option :value="8">8 秒</option>
          <option :value="20">20 秒</option>
          <option :value="60">60 秒</option>
        </select>
      </label>

      <label class="check">
        <input
          type="checkbox"
          :checked="stealth.notifyOn"
          :disabled="!stealth.notifySupported || stealth.notifyDenied"
          @change="onNotifyToggle(($event.target as HTMLInputElement).checked)"
        />
        <span>切到别的窗口时用系统通知提醒</span>
      </label>
      <p v-if="!stealth.notifySupported" class="muted note">当前浏览器不支持系统通知。</p>
      <p v-else-if="stealth.notifyDenied" class="muted note">
        浏览器已拒绝通知，需要到站点设置里重新允许。
      </p>
    </section>

    <div class="actions">
      <button class="btn btn-primary" :disabled="saving" @click="done">完成</button>
    </div>
  </ModalShell>
</template>

<style scoped>
.group {
  display: flex;
  flex-direction: column;
  gap: 12px;
  padding-bottom: 16px;
  margin-bottom: 16px;
  border-bottom: 1px solid var(--line);
}

h3 {
  margin: 0;
  font-size: 13px;
  font-weight: 600;
  color: var(--text-secondary);
}

.inline {
  display: flex;
  gap: 8px;
  align-items: center;
}

.key {
  min-width: 88px;
  font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
}

.key.on {
  border-color: var(--accent);
  color: var(--accent);
}

.note {
  font-size: 12px;
}

.theme-picks {
  display: flex;
  gap: 4px;
  padding: 3px;
  border: 1px solid var(--line-strong);
  border-radius: var(--radius);
  background: var(--bg-hover);
}

.theme-pick {
  flex: 1;
  min-width: 0;
  min-height: 36px;
  padding: 6px 8px;
  border: none;
  border-radius: 4px;
  background: transparent;
  color: var(--text-secondary);
  font-size: 13px;
}

.theme-pick.on {
  background: var(--bg-panel);
  color: var(--text);
  box-shadow: var(--shadow);
}

@media (hover: hover) {
  .theme-pick:not(.on):hover {
    color: var(--text);
  }
}

.check {
  display: flex;
  gap: 8px;
  align-items: center;
  font-size: 13px;
  color: var(--text-secondary);
}

.actions {
  display: flex;
  justify-content: flex-end;
}
</style>
