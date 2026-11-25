# Localization (#22) — work in progress

Explicit hu/en application preference on LocalData, independent of workout preference snapshots and OS language. Null is omitted from JSON to retain the legacy HMAC payload. Migration runs after verification. Existing explicit choices persist; completed legacy profiles keep their original Hungarian. New profiles choose before onboarding.

Use AppLocalization.Text/Format and paired Core Resources/AppStrings.resx and AppStrings.en.resx resources. Never change persisted category, region, equipment, status or exercise IDs through translation; localize display only. Do not change business-rule culture or date serialization.

This branch is not release-ready: complete native page translation, catalog translations, localized filtering/search, dynamic core messages, notifications, WebView, accessibility, iOS integration and physical device tests are still required.