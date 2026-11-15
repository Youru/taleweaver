import { useI18n } from 'vue-i18n'

/** The words of an ending ("@victory", "@defeat", "@currentStoryRound"…); any other stays as it is. */
export function useEndingLabel() {
  const { t } = useI18n()
  return (id: string) =>
    id === '@victory'
      ? t('workshop.endVictory')
      : id === '@defeat'
        ? t('workshop.endDefeat')
        : id === '@currentStoryRound'
          ? t('workshop.endStoryRound')
          : id
}
