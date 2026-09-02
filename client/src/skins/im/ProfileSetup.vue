<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRoute } from 'vue-router'

import ThemePicks from './ThemePicks.vue'
import { useSessionStore } from '@/stores/session'

const session = useSessionStore()
const route = useRoute()

// 顺着邀请链接来的人得知道填完就直接进群了，否则会以为自己点错了地方。
const invited = computed(() => route.name === 'invite')

const nickname = ref('')
const busy = ref(false)
const error = ref('')

const trimmed = computed(() => nickname.value.trim())
const initial = computed(() => trimmed.value.slice(0, 1) || '？')

async function submit() {
  if (!trimmed.value || busy.value) {
    return
  }

  busy.value = true
  error.value = ''

  try {
    await session.register(trimmed.value)
  } catch (cause) {
    error.value = cause instanceof Error ? cause.message : '连接失败，稍后再试'
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <div class="gate">
    <form class="box" @submit.prevent="submit">
      <div class="avatar big">{{ initial }}</div>

      <h2>完善资料</h2>
      <p class="secondary desc">
        {{
          invited
            ? '完善名称后自动加入群聊'
            : '您的名称群成员可见'
        }}
      </p>

      <label class="field">
        <span class="muted">显示名称</span>
        <input
          v-model="nickname"
          class="input"
          maxlength="20"
          placeholder="真名、花名都行"
          autofocus
        />
      </label>

      <p v-if="error" class="error">{{ error }}</p>

      <button type="submit" class="btn btn-primary go" :disabled="!trimmed || busy">
        {{ busy ? '正在进入…' : '开始使用' }}
      </button>

      <button type="button" class="btn btn-text pick" @click="nickname = session.suggest()">
        懒得想，随便给我起一个
      </button>

      <div class="theme">
        <span class="muted">主题</span>
        <ThemePicks />
      </div>
    </form>
  </div>
</template>

<style scoped>
.gate {
  display: grid;
  place-items: center;
  height: 100%;
  padding: 20px;
  padding-top: calc(20px + env(safe-area-inset-top));
  padding-bottom: calc(20px + env(safe-area-inset-bottom));
}

.box {
  display: flex;
  flex-direction: column;
  gap: 10px;
  width: min(320px, 100%);
  padding: 24px 22px;
  border: 1px solid var(--line);
  border-radius: var(--radius);
  background: var(--bg-panel);
  box-shadow: var(--shadow-lg);
}

.avatar.big {
  width: 46px;
  height: 46px;
  font-size: 18px;
}

h2 {
  margin: 4px 0 0;
  font-size: 17px;
}

.desc {
  margin: 0;
  font-size: 13px;
}

.field {
  margin-top: 4px;
}

.field span {
  font-size: 12px;
}

.error {
  margin: 0;
  color: var(--danger);
  font-size: 12px;
}

.go {
  margin-top: 2px;
}

.pick {
  align-self: center;
  font-size: 12px;
}

.theme {
  display: flex;
  flex-direction: column;
  gap: 6px;
  margin-top: 8px;
  padding-top: 12px;
  border-top: 1px solid var(--line);
}

.theme span {
  font-size: 12px;
}
</style>
