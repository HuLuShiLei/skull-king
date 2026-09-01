<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'

import ModalShell from './ModalShell.vue'
import { api } from '@/api/http'
import { useLobbyStore } from '@/stores/lobby'
import { useSessionStore } from '@/stores/session'

const props = defineProps<{ tab: 'create' | 'join' }>()
const emit = defineEmits<{ close: [] }>()

const lobby = useLobbyStore()
const session = useSessionStore()
const router = useRouter()

const active = ref(props.tab)
const busy = ref(false)
const error = ref('')

const form = ref({
  name: `${session.nickname}的协作组`,
  isPublic: true,
  maxPlayers: 6,
  maxRounds: 10,
  turnSeconds: 60,
  password: '',
})

const joinCode = ref('')
const joinPassword = ref('')

async function submitCreate() {
  busy.value = true
  error.value = ''

  try {
    const code = await lobby.create({
      name: form.value.name,
      isPublic: form.value.isPublic,
      maxPlayers: form.value.maxPlayers,
      maxRounds: form.value.maxRounds,
      turnSeconds: form.value.turnSeconds,
      password: form.value.password || undefined,
    })

    emit('close')
    await router.push({ name: 'room', params: { code } })
  } catch (e) {
    error.value = e instanceof Error ? e.message : '创建失败'
  } finally {
    busy.value = false
  }
}

async function submitJoin() {
  const code = joinCode.value.trim().toUpperCase()

  if (code.length !== 6) {
    error.value = '群号是 6 位字符'
    return
  }

  busy.value = true
  error.value = ''

  try {
    const probe = await api.probeRoom(code)

    if (!probe.exists) {
      error.value = '找不到这个群'
      return
    }

    emit('close')
    await router.push({
      name: 'room',
      params: { code },
      query: joinPassword.value ? { pw: joinPassword.value } : undefined,
    })
  } catch (e) {
    error.value = e instanceof Error ? e.message : '加入失败'
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <ModalShell :title="active === 'create' ? '新建群聊' : '加入群聊'" @close="emit('close')">
    <div class="tabs">
      <button class="tab" :class="{ on: active === 'create' }" @click="active = 'create'">
        新建
      </button>
      <button class="tab" :class="{ on: active === 'join' }" @click="active = 'join'">
        输入群号
      </button>
    </div>

    <form v-if="active === 'create'" class="form" @submit.prevent="submitCreate">
      <label class="field">
        <span>群名称</span>
        <input v-model="form.name" class="input" maxlength="40" />
      </label>

      <div class="grid">
        <label class="field">
          <span>成员上限</span>
          <select v-model.number="form.maxPlayers" class="input">
            <option v-for="n in 7" :key="n" :value="n + 1">{{ n + 1 }} 人</option>
          </select>
        </label>

        <label class="field">
          <span>议程轮数</span>
          <select v-model.number="form.maxRounds" class="input">
            <option v-for="n in 10" :key="n" :value="n">{{ n }} 轮</option>
          </select>
        </label>
      </div>

      <div class="grid">
        <label class="field">
          <span>单步限时</span>
          <select v-model.number="form.turnSeconds" class="input">
            <option :value="0">不限时</option>
            <option :value="30">30 秒</option>
            <option :value="60">60 秒</option>
            <option :value="120">120 秒</option>
          </select>
        </label>

        <label class="field">
          <span>入群口令（可选）</span>
          <input v-model="form.password" class="input" maxlength="20" />
        </label>
      </div>

      <label class="check">
        <input v-model="form.isPublic" type="checkbox" />
        <span>允许在列表中被搜索到</span>
      </label>

      <p v-if="error" class="error">{{ error }}</p>

      <div class="actions">
        <button type="button" class="btn" @click="emit('close')">取消</button>
        <button type="submit" class="btn btn-primary" :disabled="busy">创建</button>
      </div>
    </form>

    <form v-else class="form" @submit.prevent="submitJoin">
      <label class="field">
        <span>群号</span>
        <input
          v-model="joinCode"
          class="input code-input"
          maxlength="6"
          placeholder="6 位字符"
          autofocus
        />
      </label>

      <label class="field">
        <span>入群口令（如果需要）</span>
        <input v-model="joinPassword" class="input" maxlength="20" />
      </label>

      <p class="hint muted">也可以直接打开别人发来的邀请链接，形如 /j/ABC234</p>
      <p v-if="error" class="error">{{ error }}</p>

      <div class="actions">
        <button type="button" class="btn" @click="emit('close')">取消</button>
        <button type="submit" class="btn btn-primary" :disabled="busy">加入</button>
      </div>
    </form>
  </ModalShell>
</template>

<style scoped>
.tabs {
  display: flex;
  gap: 4px;
  margin-bottom: 16px;
  border-bottom: 1px solid var(--line);
}

.tab {
  padding: 7px 14px;
  border: none;
  border-bottom: 2px solid transparent;
  background: transparent;
  color: var(--text-secondary);
}

.tab.on {
  border-bottom-color: var(--accent);
  color: var(--accent);
}

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

.check {
  display: flex;
  gap: 8px;
  align-items: center;
  font-size: 13px;
  color: var(--text-secondary);
}

.code-input {
  font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
  font-size: 18px;
  letter-spacing: 4px;
  text-transform: uppercase;
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
</style>
