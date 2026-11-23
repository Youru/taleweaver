<script setup lang="ts">
import { computed, nextTick, onMounted, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute } from 'vue-router'
import { storeToRefs } from 'pinia'
import { usePreferences } from '@/shared/lib/preferences'
import ManualText from '../components/ManualText.vue'
import { en } from '../content/en'
import { fr } from '../content/fr'

/** The GM's manual (/manual): the section read is in the address. */
const { t } = useI18n()
const route = useRoute()
const { language } = storeToRefs(usePreferences())
const manual = computed(() => (language.value === 'fr' ? fr : en))
const sections = computed(() => manual.value.groups.flatMap((group) => group.sections))
const titles = computed(() => new Map(sections.value.map((section) => [section.id, section.title])))

async function showSection() {
  await nextTick()
  if (route.hash) document.getElementById(route.hash.slice(1))?.scrollIntoView()
  else window.scrollTo(0, 0)
}
onMounted(showSection)
// The whole address: the link of the section already read brings it back too
watch(() => route.fullPath, showSection)
</script>

<template>
  <main class="manual">
    <nav class="contents" :aria-label="t('manual.contents')">
      <template v-for="group in manual.groups" :key="group.title">
        <p>{{ group.title }}</p>
        <ul>
          <li v-for="section in group.sections" :key="section.id">
            <RouterLink
              :to="{ hash: `#${section.id}` }"
              active-class=""
              exact-active-class=""
              :class="{ here: route.hash === `#${section.id}` }"
              @click="showSection"
            >
              {{ section.title }}
            </RouterLink>
          </li>
        </ul>
      </template>
    </nav>

    <article class="book">
      <h1>{{ t('manual.title') }}</h1>
      <section v-for="section in sections" :id="section.id" :key="section.id" :aria-labelledby="`${section.id}-title`">
        <h2 :id="`${section.id}-title`">{{ section.title }}</h2>
        <p class="lead"><ManualText :text="section.lead" :titles="titles" /></p>
        <template v-for="(part, i) in section.parts" :key="i">
          <h3 v-if="part.title">{{ part.title }}</h3>
          <figure v-if="part.figure" class="shot">
            <img
              :src="`/help/${language}/${part.figure.image}.png`"
              :alt="part.figure.caption"
              width="1280"
              height="800"
              loading="lazy"
            />
            <figcaption>
              <span class="caption">{{ part.figure.caption }}</span>
              <ol v-if="part.figure.legend" class="legend">
                <li v-for="(item, j) in part.figure.legend" :key="j">
                  <span class="mark" aria-hidden="true">{{ j + 1 }}</span>
                  <ManualText :text="item" :titles="titles" />
                </li>
              </ol>
            </figcaption>
          </figure>
          <p v-if="part.text"><ManualText :text="part.text" :titles="titles" /></p>
          <ol v-if="part.steps">
            <li v-for="(step, j) in part.steps" :key="j"><ManualText :text="step" :titles="titles" /></li>
          </ol>
          <ul v-if="part.list">
            <li v-for="(item, j) in part.list" :key="j"><ManualText :text="item" :titles="titles" /></li>
          </ul>
          <p v-if="part.note" class="note"><ManualText :text="part.note" :titles="titles" /></p>
        </template>
      </section>
    </article>
  </main>
</template>

<style scoped>
.manual {
  display: grid;
  grid-template-columns: 18rem minmax(0, 1fr);
  align-items: start;
  gap: 1.5rem;
  max-width: 72rem;
  margin: 0 auto;
  padding: 0 1rem 2rem;
}
.contents,
.book {
  box-sizing: border-box;
  background: rgb(251 245 232 / 0.88);
  border: 1px solid var(--p-content-border-color);
  border-radius: 6px;
}
/* The screenshots: the marks of the legend look like those drawn on the picture */
.shot {
  margin: 0.75rem 0 1.25rem;
}
.shot img {
  display: block;
  width: 100%;
  height: auto;
  border: 1px solid var(--p-content-border-color);
  border-radius: 6px;
  box-shadow: 0 2px 8px rgb(0 0 0 / 0.18);
}
.caption {
  display: block;
  margin: 0.4rem 0 0.3rem;
  color: var(--p-text-muted-color);
  font-style: italic;
}
.legend {
  margin: 0;
  padding: 0;
  list-style: none;
  display: grid;
  gap: 0.3rem;
}
.legend li {
  display: flex;
  align-items: baseline;
  gap: 0.5rem;
}
.mark {
  flex: none;
  width: 1.4rem;
  height: 1.4rem;
  border-radius: 50%;
  background: #e8a317;
  color: #1a1a1a;
  font:
    700 0.8rem/1.4rem system-ui,
    sans-serif;
  text-align: center;
}
.contents {
  position: sticky;
  top: 0.75rem;
  max-height: calc(100dvh - 1.5rem);
  overflow-y: auto;
  padding: 0.5rem 1rem 1rem;
  font-size: 0.92rem;
}
.contents p {
  margin: 0.75rem 0 0.25rem;
  color: var(--p-text-muted-color);
  font-family: var(--mid-title-font);
  font-variant: small-caps;
  font-weight: 700;
}
.contents ul {
  margin: 0;
  padding: 0;
  list-style: none;
}
.contents a {
  display: block;
  padding: 0.2rem 0.4rem;
  border-radius: 4px;
  color: inherit;
  text-decoration: none;
}
.contents a:hover,
.contents a.here {
  background: var(--p-highlight-background);
  color: var(--mid-crimson);
}
.book {
  padding: 1rem 2rem 2rem;
  line-height: 1.55;
}
.book h1 {
  margin: 0;
}
.book section {
  scroll-margin-top: 0.75rem;
}
.book section + section {
  margin-top: 2rem;
  border-top: 1px solid var(--p-content-border-color);
}
.book h3 {
  margin: 1.25rem 0 0.25rem;
  font-size: 1.05rem;
}
.book li {
  margin-bottom: 0.3rem;
}
.lead {
  font-family: var(--mid-book-font);
  font-size: 1.1rem;
}
.note {
  padding: 0.5rem 0.75rem;
  border-left: 3px solid var(--mid-crimson);
  background: rgb(227 210 174 / 0.45);
}
@media (max-width: 50rem) {
  .manual {
    grid-template-columns: minmax(0, 1fr);
  }
  .contents {
    position: static;
    max-height: none;
  }
  .book {
    padding: 0.75rem 1rem 1.5rem;
  }
}
</style>
