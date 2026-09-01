<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'

import { api } from '@/api/http'

const props = defineProps<{ code: string }>()

const route = useRoute()
const router = useRouter()
const state = ref<'checking' | 'password' | 'missing'>('checking')
const roomName = ref('')
const password = ref('')
const error = ref('')

const normalized = props.code.trim().toUpperCase()

onMounted(async () => {
  try {
    const probe = await api.probeRoom(normalized)

    if (!probe.exists) {
      state.value = 'missing'
      return
    }

    roomName.value = probe.name

    // 链接里本来就带着口令，直接往里走，不用再拦一道。
    if (typeof route.query.pw === 'string' && route.query.pw) {
      password.value = route.query.pw
      enter()
      return
    }

    if (probe.hasPassword) {
      state.value = 'password'
      return
    }

    await router.replace({ name: 'room', params: { code: normalized } })
  } catch (e) {
    error.value = e instanceof Error ? e.message : '打开失败'
    state.value = 'missing'
  }
})

function enter() {
  void router.replace({
    name: 'room',
    params: { code: normalized },
    query: password.value ? { pw: password.value } : undefined,
  })
}
</script>

<template>
  <div class="invite">
    <div v-if="state === 'checking'" class="muted">正在打开…</div>

    <form v-else-if="state === 'password'" class="box" @submit.prevent="enter">
      <h2>{{ roomName }}</h2>
      <p class="secondary">这个群需要口令</p>

      <input v-model="password" class="input" maxlength="20" autofocus />
      <button type="submit" class="btn btn-primary">进入</button>
    </form>

    <div v-else class="box">
      <h2>找不到这个群</h2>
      <p class="secondary">{{ error || '链接可能已失效，或者群已经解散。' }}</p>
      <button class="btn" @click="router.replace('/')">返回</button>
    </div>
  </div>
</template>

<style scoped>
.invite {
  display: grid;
  place-items: center;
  flex: 1;
  padding: 40px;
}

.box {
  display: flex;
  flex-direction: column;
  gap: 10px;
  width: 280px;
  padding: 20px;
  border: 1px solid var(--line);
  border-radius: var(--radius-lg);
  background: var(--bg-panel);
  text-align: center;
}

h2 {
  margin: 0;
  font-size: 15px;
  font-weight: 600;
}

.box p {
  margin: 0;
  font-size: 13px;
}
</style>
