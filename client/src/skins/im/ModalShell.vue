<script setup lang="ts">
defineProps<{ title: string; width?: number }>()
const emit = defineEmits<{ close: [] }>()
</script>

<template>
  <div class="backdrop" @click.self="emit('close')">
    <section class="modal" :style="{ width: `${width ?? 420}px` }">
      <header>
        <h2>{{ title }}</h2>
        <button class="btn btn-text" @click="emit('close')">×</button>
      </header>

      <div class="body">
        <slot />
      </div>
    </section>
  </div>
</template>

<style scoped>
.backdrop {
  position: fixed;
  inset: 0;
  display: grid;
  place-items: center;
  background: rgba(31, 35, 41, 0.28);
  z-index: 50;
}

.modal {
  max-width: calc(100vw - 32px);
  max-height: calc(100vh - 64px);
  display: flex;
  flex-direction: column;
  border-radius: var(--radius-lg);
  background: var(--bg-panel);
  box-shadow: var(--shadow-lg);
}

header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 14px 16px;
  border-bottom: 1px solid var(--line);
}

h2 {
  margin: 0;
  font-size: 15px;
  font-weight: 600;
}

.body {
  padding: 16px;
  overflow-y: auto;
}
</style>
