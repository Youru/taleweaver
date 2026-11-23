<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import Button from 'primevue/button'
import ConfirmDialog from 'primevue/confirmdialog'

/**
 * The app's one confirmation (confirm.require): a page of the book, its crimson banner for a title,
 * the question in book type, and the two answers. A danger (accept severity 'danger') gets a bin for icon.
 */
const { t } = useI18n()
</script>

<template>
  <ConfirmDialog class="action-confirm">
    <template #container="{ message, acceptCallback, rejectCallback }">
      <header class="banner">
        <i
          :class="
            message.icon ?? (message.acceptProps?.severity === 'danger' ? 'pi pi-trash' : 'pi pi-question-circle')
          "
          aria-hidden="true"
        />
        <span>{{ message.header ?? t('common.confirm') }}</span>
      </header>
      <p class="question">{{ message.message }}</p>
      <footer class="answers">
        <Button
          :label="message.rejectProps?.label ?? t('common.cancel')"
          severity="secondary"
          text
          @click="rejectCallback"
        />
        <Button
          :label="message.acceptProps?.label ?? t('common.confirm')"
          :severity="message.acceptProps?.severity === 'danger' ? 'danger' : undefined"
          autofocus
          @click="acceptCallback"
        />
      </footer>
    </template>
  </ConfirmDialog>
</template>

<style>
/* Teleported to the body: not scoped */
.action-confirm.p-dialog {
  width: min(26rem, calc(100vw - 2rem));
  overflow: hidden;
  border: 1px solid rgb(80 50 25 / 0.45);
  border-radius: 6px;
  background: #f3e9d8 var(--mid-parchment) center / cover;
  box-shadow:
    0 12px 40px rgb(30 15 5 / 0.45),
    inset 0 0 40px rgb(90 55 20 / 0.2);
}
.action-confirm .banner {
  display: flex;
  align-items: center;
  gap: 0.6rem;
  padding: 0.75rem 1.25rem;
  background: var(--mid-crimson) var(--mid-banner) left center / cover;
  color: var(--mid-cream);
  font-family: var(--mid-title-font);
  font-variant: small-caps;
  font-size: 1.2rem;
  letter-spacing: 0.03em;
}
.action-confirm .question {
  margin: 0;
  padding: 1.25rem 1.5rem 0.5rem;
  color: var(--mid-ink);
  font-family: var(--mid-book-font);
  font-size: 1.08rem;
  line-height: 1.45;
}
.action-confirm .answers {
  display: flex;
  justify-content: flex-end;
  gap: 0.5rem;
  padding: 0.75rem 1.25rem 1.1rem;
}
.action-confirm .answers .p-button-text {
  color: var(--mid-ink);
}
.action-confirm .answers .p-button-danger:not(.p-button-text) {
  background: #8e1f1f;
  border-color: #8e1f1f;
}
.action-confirm .answers .p-button-danger:not(.p-button-text):hover {
  background: var(--mid-crimson);
  border-color: var(--mid-crimson);
}
</style>
