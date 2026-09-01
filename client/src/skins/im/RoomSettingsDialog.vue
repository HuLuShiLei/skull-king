<script setup lang="ts">
import { computed, ref } from 'vue'

import FieldHint from './FieldHint.vue'
import ModalShell from './ModalShell.vue'
import { settingHints } from './settingHints'
import { RoomStatusValues } from '@/api/types'
import { useRoomStore } from '@/stores/room'

const emit = defineEmits<{ close: [] }>()

const room = useRoomStore()

// 对局进行中只放行限时：其余几项会牵动座位和轮数，中途改会把牌局搞乱。
const playing = computed(() => room.state?.status === RoomStatusValues.Playing)

const settings = room.state?.settings
const form = ref({
  name: settings?.name ?? '',
  isPublic: settings?.isPublic ?? true,
  maxPlayers: settings?.maxPlayers ?? 6,
  maxRounds: settings?.maxRounds ?? 10,
  turnSeconds: settings?.turnSeconds ?? 60,
})

const busy = ref(false)
const error = ref('')

async function submit() {
  busy.value = true
  error.value = ''

  const ok = await room.updateSettings(
    playing.value
      ? { turnSeconds: form.value.turnSeconds }
      : {
          name: form.value.name,
          isPublic: form.value.isPublic,
          maxPlayers: form.value.maxPlayers,
          maxRounds: form.value.maxRounds,
          turnSeconds: form.value.turnSeconds,
        },
  )

  busy.value = false

  if (ok) {
    emit('close')
  } else {
    error.value = room.lastError || '保存失败'
  }
}
</script>

<template>
  <ModalShell title="群设置" :width="440" @close="emit('close')">
    <form class="form" @submit.prevent="submit">
      <label class="field">
        <span>群名称 <FieldHint :text="settingHints.name" /></span>
        <input v-model="form.name" class="input" maxlength="40" :disabled="playing" />
      </label>

      <div class="grid">
        <label class="field">
          <span>成员上限 <FieldHint :text="settingHints.maxPlayers" /></span>
          <select v-model.number="form.maxPlayers" class="input" :disabled="playing">
            <option v-for="n in 7" :key="n" :value="n + 1">{{ n + 1 }} 人</option>
          </select>
        </label>

        <label class="field">
          <span>议程轮数 <FieldHint :text="settingHints.maxRounds" /></span>
          <select v-model.number="form.maxRounds" class="input" :disabled="playing">
            <option v-for="n in 10" :key="n" :value="n">{{ n }} 轮</option>
          </select>
        </label>
      </div>

      <label class="field">
        <span>单步限时 <FieldHint :text="settingHints.turnSeconds" /></span>
        <select v-model.number="form.turnSeconds" class="input">
          <option :value="0">不限时</option>
          <option :value="30">30 秒</option>
          <option :value="60">60 秒</option>
          <option :value="120">120 秒</option>
          <option :value="300">5 分钟</option>
        </select>
      </label>

      <label class="check">
        <input v-model="form.isPublic" type="checkbox" :disabled="playing" />
        <span>允许在列表中被搜索到</span>
        <FieldHint :text="settingHints.isPublic" />
      </label>

      <p v-if="playing" class="hint muted">进行中只能改限时。</p>
      <p v-if="error" class="error">{{ error }}</p>

      <div class="actions">
        <button type="button" class="btn" @click="emit('close')">取消</button>
        <button type="submit" class="btn btn-primary" :disabled="busy">保存</button>
      </div>
    </form>
  </ModalShell>
</template>

<style scoped>
.form {
  display: flex;
  flex-direction: column;
  gap: 14px;
}

.grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 12px;
}

/* 标题行要装下问号按钮，全局的 .field > span 只是纯文本 */
.field > span {
  display: flex;
  gap: 5px;
  align-items: center;
}

/* FieldHint 用 closest('.field, .check') 量宽度 */
.field,
.check {
  position: relative;
}

.check {
  display: flex;
  gap: 8px;
  align-items: center;
  font-size: 13px;
  color: var(--text-secondary);
}

.hint {
  margin: 0;
  font-size: 12px;
}

.error {
  margin: 0;
  font-size: 13px;
  color: var(--danger);
}

.actions {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
}

@media (max-width: 800px) {
  .grid {
    grid-template-columns: 1fr;
  }
}
</style>
