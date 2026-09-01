<script setup lang="ts">
import { ref } from 'vue'

import ModalShell from './ModalShell.vue'
import { useSessionStore } from '@/stores/session'
import { useStealthStore } from '@/stores/stealth'

const emit = defineEmits<{ close: [] }>()

const session = useSessionStore()
const stealth = useStealthStore()

const nickname = ref(session.nickname)
const capturing = ref(false)
const saving = ref(false)

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
