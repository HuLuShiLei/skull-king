<script setup lang="ts">
import { ref } from 'vue'

import {
  getThemePreference,
  setThemePreference,
  THEME_OPTIONS,
  type ThemePreference,
} from '@/theme'

const theme = ref(getThemePreference())

function pick(next: ThemePreference) {
  setThemePreference(next)
  theme.value = next
}
</script>

<template>
  <div class="theme-picks" role="radiogroup" aria-label="主题">
    <button
      v-for="option in THEME_OPTIONS"
      :key="option.id"
      type="button"
      class="theme-pick"
      role="radio"
      :aria-checked="theme === option.id"
      :class="{ on: theme === option.id }"
      @click="pick(option.id)"
    >
      {{ option.label }}
    </button>
  </div>
</template>

<style scoped>
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
</style>
