# Принципи неймінгу папок, асетів і об'єктів проєкту **Stitches**

Цей документ визначає єдиний стандарт іменування папок, асетів, C#-скриптів та об'єктів ієрархії сцен у Unity-проєкті **Stitches**. Стандарт базується на офіційних рекомендаціях **Unity Project Organization**, **Unity C# Style Guide** та індустріальних практиках розробки ігор.

---

## 1. Загальні правила

1. **Тільки англійська мова та ASCII-символи** — жодної кирилиці (`небо.png` ❌ → `SPR_Bg_NightSky.png` ✅), емодзі чи спецсимволів (`(`, `)`, `&`, `#`, `%`, `~`).
2. **Жодних пробілів у назвах папок і файлів** — замість пробілів використовуємо `PascalCase` або підкреслення `_` як роздільник логічних секцій (`Pumpkin 1.png` ❌ → `SPR_Prop_Pumpkin_01.png` ✅).
3. **Єдина нумерація з провідним нулем** — послідовності кадрів і варіантів починаються з `_01` (двозначний індекс: `_01`, `_02`, ..., `_10`), щоб файли завжди правильно сортувалися в алфавітному порядку.
4. **Без слів-паразитів і дублікатів копій** — уникаємо назв типу `New`, `Copy`, `Final`, `Final2`, `sprite_0 1`, `idle 2 (2)`.
5. **Усі тимчасові файли та бекапи заборонені в репозиторії** — наприклад, `*.png~` із графічних редакторів не додаються у `Assets/`.
6. **Орфографічна точність** — перевіряємо правопис англійських термінів (`Sceleton` ❌ → `Skeleton` ✅, `Grass_rightt` ❌ → `SPR_Tile_Grass_Right` ✅, `GroundChek` ❌ → `groundCheck` ✅).

---

## 2. Структура та неймінг папок

### Основні принципи
- **Ізоляція власного контенту в `Assets/_Project/`** — ведуче підкреслення `_` закріплює папку проєкту вгорі вікна Project у Unity та чітко відокремлює файли гри від системних папок Unity (`Settings`, `Welcome`) і сторонніх пакетів (`TextMesh Pro`, `Plugins`).
- **Регістр папок**: `PascalCase` (`DeathSmoke`, `RetractableSpikes`, `Characters`).
- **Множина для контейнерів (колекцій)**: `Scripts`, `Sprites`, `Animations`, `Scenes`, `Enemies`, `Characters`, `Props`, `Tiles`, `Decorations`.
- **Однина для конкретної сутності або домену**: `Player`, `Frank`, `Skeleton`, `Spirit`, `Zombie`, `UI`, `VFX`, `Audio`, `Combat`, `Narrative`, `Progression`.
- **Помірна вкладеність** — не більше 4–5 рівнів, групування за типом асету на верхньому рівні та за функціональним доменом всередині.

### Затверджена структура `Assets/_Project/`

```text
Assets/
├── _Project/
│   ├── Animations/                 # Усі AnimationClip та AnimatorController
│   │   ├── Characters/
│   │   │   └── Player/
│   │   ├── Enemies/
│   │   │   ├── Common/             # Спільні анімації для кількох ворогів (напр. Death)
│   │   │   ├── Skeleton/
│   │   │   ├── Spirit/
│   │   │   └── Zombie/
│   │   └── Environment/
│   │       └── Hazards/
│   ├── Art/                        # Усі візуальні ресурси
│   │   └── Sprites/
│   │       ├── Characters/
│   │       │   ├── Frank/
│   │       │   └── Player/
│   │       │       ├── Idle/
│   │       │       ├── Jump/
│   │       │       └── Run/
│   │       ├── Combat/             # Снаряди, зброя, підбирані боєприпаси
│   │       ├── Enemies/
│   │       │   ├── Skeleton/
│   │       │   ├── Spirit/
│   │       │   └── Zombie/
│   │       ├── Environment/
│   │       │   ├── Backgrounds/
│   │       │   ├── Hazards/
│   │       │   ├── Props/
│   │       │   │   ├── Decorations/
│   │       │   │   └── Lab/
│   │       │   └── Tiles/
│   │       ├── UI/
│   │       └── VFX/
│   │           └── DeathSmoke/
│   ├── Scenes/
│   │   ├── UI/                     # Сцени інтерфейсу (меню, екран завантаження)
│   │   └── Levels/                 # Ігрові рівні (додаються по мірі розробки)
│   └── Scripts/                    # C#-скрипти, згруповані за модулями/доменами
│       ├── Audio/
│       ├── Combat/
│       ├── Enemies/
│       ├── Environment/
│       ├── Items/
│       ├── Narrative/
│       ├── Player/
│       ├── Progression/
│       └── UI/
├── Settings/                       # Системні налаштування URP / Input System
└── TextMesh Pro/                   # Базові ресурси та шрифти TMP
```

> **Примітка:** Документація проєкту (включно з цим файлом) зберігається поза `Assets/` — у кореневій папці репозиторію `Docs/`, щоб не перевантажувати Unity Asset Database.

---

## 3. Неймінг асетів (Asset Naming Convention)

Формат імені асету:

```text
<PREFIX>_<Domain/Category>_<Entity/Descriptor>_<State/Variant>_<Index>
```

### Таблиця префіксів асетів

| Тип асету | Розширення | Префікс | Шаблон | Приклад у проєкті |
|---|---|---|---|---|
| Спрайт | `.png` | `SPR_` | `SPR_<Category>_<Name>[_<Action/State>][_<##>]` | `SPR_Player_Run_01.png`, `SPR_UI_HealthBar_Background.png` |
| Анімаційний кліп | `.anim` | `ANIM_` | `ANIM_<Entity>_<Action>` | `ANIM_Player_Jump.anim`, `ANIM_Enemy_Skeleton_Run.anim` |
| Animator Controller | `.controller` | `AC_` | `AC_<Entity>` | `AC_Player.controller`, `AC_Enemy_Spirit.controller` |
| Сцена | `.unity` | `SCN_` | `SCN_<SceneName>` | `SCN_MainMenu.unity`, `SCN_Level_01.unity` |
| Префаб | `.prefab` | `PFB_` | `PFB_<Category>_<Name>` | `PFB_Enemy_Skeleton.prefab`, `PFB_Combat_Arrow.prefab` |
| Матеріал | `.mat` | `MAT_` | `MAT_<Usage>_<Name>` | `MAT_Sprite_Outline.mat` |
| Фізичний матеріал 2D | `.physicsMaterial2D` | `PM2D_` | `PM2D_<Name>` | `PM2D_Player_NoFriction.physicsMaterial2D` |
| Тайл (Tilemap Tile) | `.asset` | `TILE_` | `TILE_<Biome>_<Name>` | `TILE_Castle_Wall.asset` |
| Тайлова палітра | `.prefab` | `PAL_` | `PAL_<Biome>` | `PAL_Castle.prefab` |
| ScriptableObject | `.asset` | `SO_` | `SO_<Category>_<Name>` | `SO_EnemyConfig_Zombie.asset` |
| Звуковий ефект | `.wav` / `.ogg` | `SFX_` | `SFX_<Entity>_<Action>[_<##>]` | `SFX_Player_Footstep_01.wav` |
| Фонова музика / ембієнт | `.mp3` / `.ogg` | `MUS_` / `AMB_` | `AMB_<Location>` | `AMB_Level01_Castle.mp3` |
| Шейдер / Shader Graph | `.shader` / `.shadergraph` | `SHD_` | `SHD_<EffectName>` | `SHD_HitFlash.shadergraph` |

### Додаткові правила для спрайтів і плейсхолдерів
- **Тимчасові спрайти (плейсхолдери)** завжди позначаються суфіксом `_Placeholder`, щоб їх легко було знайти через пошук `t:Sprite Placeholder` і замінити фінальним артом:
  - `SPR_Tile_Dirt_Placeholder.png`
  - `SPR_Tile_GrassSlope_Left_Placeholder.png`
- **Внутрішні підспрайти (Sliced / Multiple Sprite Mode)** зберігають ім'я батьківського файлу + суфікс індексу (`SPR_Player_Idle_01_0`), що підтримує порядок при розкритті спрайта у Unity Project Window.

### Стани та параметри Animator
- **Стани (States)**: короткий `PascalCase` без зайвого дублювання імені персонажа, якщо контролер уже належить конкретній сутності:
  - `AC_Player`: `Idle`, `Run`, `Jump`, `Fall`
  - `AC_Enemy_*`: `Run`, `Death`
- **Параметри (Parameters)**: `PascalCase`, для булевих — префікс `Is` (`IsGrounded`, `IsMoving`), для тригерів — дієслово (`Die`, `Attack`, `Jump`).

---

## 4. Неймінг C#-скриптів та коду (Unity C# Style Guide)

### Файли та класи
- **Ім'я файлу завжди 1-в-1 збігається з іменем основного класу** (обов'язкова вимога Unity для `MonoBehaviour` та `ScriptableObject`).
- **Жодних префіксів типу `SCR_` у назвах `.cs` файлів** — C#-класи іменуються чистим `PascalCase` в однині.
- **Чіткі суфікси ролей**:
  - `Controller` — керує поведінкою сутності або екрана (`PlayerController`, `EnemyController`, `MainMenuController`, `FrankVisionController`).
  - `Manager` — координує підсистему рівня або гри (`LevelAmbientAudioManager`).
  - `Tracker` / `Service` / `System` — стан, прогресія, бізнес-логіка (`BodyPartsTracker`).
  - `Projectile` / `Pickup` / `Collectible` / `Platform` — конкретна ігрова роль (`ArrowProjectile`, `ArrowPickup`, `CollectibleItem`, `MovingPlatform`, `OneWayPlatform`).
- **Простори імен (`namespace`)** дзеркально відповідають структурі папок усередині `Scripts/`:
  - `Stitches.Player`, `Stitches.Enemies`, `Stitches.Combat`, `Stitches.Environment`, `Stitches.Items`, `Stitches.Narrative`, `Stitches.Progression`, `Stitches.Audio`, `Stitches.UI`.

### Елементи всередині C#-коду

| Елемент | Стиль | Приклад |
|---|---|---|
| Клас, структура, `enum`, інтерфейс | `PascalCase` (інтерфейси з `I`) | `PlayerController`, `EnemyType`, `IDamageable` |
| Публічні методи та властивості | `PascalCase` | `TakeDamage()`, `IsDead`, `TotalPartsCollected` |
| Константи (`const`, `static readonly`) | `PascalCase` | `JumpCooldownDuration`, `AnimStateIdle` |
| Приватні поля екземпляра | `_camelCase` | `_rb`, `_isGrounded`, `_jumpBufferCounter` |
| Серіалізовані поля в інспекторі | `camelCase` | `moveSpeed`, `jumpForce`, `groundCheck` |
| Локальні змінні та параметри | `camelCase` | `horizontalInput`, `targetPosition` |
| Булеві змінні та властивості | Префікс `is` / `has` / `can` / `should` | `_isJumping`, `HasHead`, `CanSeePlayer()` |
| Події (`event`) | `On` + дієслово в минулому/теперішньому часі | `OnPartCollected` |

> **Порада щодо рефакторингу:** При перейменуванні серіалізованого поля в Unity додавайте атрибут `[FormerlySerializedAs("oldName")]` із простору імен `UnityEngine.Serialization`, щоб не втратити значення, налаштовані в Інспекторі.

---

## 5. Неймінг об'єктів у Scene Hierarchy (Ієрархія сцени)

### Організація кореневих контейнерів
Для підтримання чистоти сцени кореневі порожні об'єкти-роздільники (Transform у `(0, 0, 0)`) іменуються в `UPPERCASE` з оформленням `--- NAME ---`:

```text
SCN_MainMenu
├── --- SYSTEMS ---
│   ├── SYS_MainMenuController
│   └── EventSystem
├── --- CAMERAS ---
│   └── Main Camera
└── --- UI ---
    └── UI_Canvas_MainMenu
        ├── IMG_Background
        ├── TXT_Logo_Stitches
        └── BTN_Continue
            └── TXT_ContinueLabel
```

### Префікси об'єктів в ієрархії

| Категорія | Префікс | Приклад |
|---|---|---|
| Системні менеджери / контролери | `SYS_` | `SYS_MainMenuController`, `SYS_LevelAmbientAudioManager` |
| Гравці та NPC | `CHR_` | `CHR_Player`, `CHR_Frank_Full` |
| Вороги | `ENM_` | `ENM_Skeleton_01`, `ENM_Spirit_02` |
| Оточення та платформи | `ENV_` | `ENV_MovingPlatform_01`, `ENV_OneWayPlatform_02` |
| Тригери та інтерактивні зони | `TRG_` | `TRG_KillZone`, `TRG_LevelTransitionDoor` |
| Точки спавну / патрулювання / перевірки | `PT_` | `PT_GroundCheck`, `PT_ArrowSpawn`, `PT_Patrol_01` |
| Візуальні ефекти | `VFX_` | `VFX_LightningStrike`, `VFX_DeathSmoke` |
| Аудіо-джерела | `AUD_` | `AUD_PlayerWalk`, `AUD_PlayerLanding` |
| UI Canvas | `UI_Canvas_` | `UI_Canvas_MainMenu`, `UI_Canvas_HUD` |
| UI Панель / Контейнер | `PNL_` | `PNL_Dialogue`, `PNL_HealthBar` |
| UI Кнопка | `BTN_` | `BTN_Continue`, `BTN_SkipCutscene` |
| UI Текст (TextMeshPro) | `TXT_` | `TXT_Logo_Stitches`, `TXT_ArrowCount` |
| UI Зображення | `IMG_` | `IMG_Background`, `IMG_HealthFill` |
| UI Слайдер / Прогрес-бар | `SLD_` / `BAR_` | `BAR_Health`, `SLD_MasterVolume` |

> **Виняток для системних об'єктів Unity:** `Main Camera` та `EventSystem` допускається залишати зі стандартними іменами Unity для сумісності з шаблонами та інструментами.

---

## 6. Приклади приведення до стандарту («Було → Стало»)

| Проблема у старому проєкті | Стара назва | Нова назва за стандартом |
|---|---|---|
| Кирилиця у файлі | `Art/Placeholders/небо.png` | `Art/Sprites/Environment/Backgrounds/SPR_Bg_NightSky.png` |
| Орфографічні помилки | `Sceleton.controller`, `Grass_rightt.png` | `AC_Enemy_Skeleton.controller`, `SPR_Tile_Grass_Right.png` |
| Пробіли та дублікати копій | `idle 2 (2).png`, `sprite_0 1.png` | `SPR_Player_Idle_03.png`, `SPR_Prop_LabTube_Medium.png` |
| Непослідовний регістр | `Player_idle.anim`, `Player_Jump.anim` | `ANIM_Player_Idle.anim`, `ANIM_Player_Jump.anim` |
| Множина для компонента | `MovingPlatforms.cs` | `MovingPlatform.cs` |
| Загальні імена без ролі | `Player.cs`, `Enemy.cs`, `Arrow.cs`, `Door.cs` | `PlayerController.cs`, `EnemyController.cs`, `ArrowProjectile.cs`, `LevelTransitionDoor.cs` |
| Неінформативні імена кадрів | `Death_Anim/sprite_0.png` .. `sprite_6.png` | `VFX/DeathSmoke/SPR_VFX_DeathSmoke_01.png` .. `_07.png` |
| Тимчасові бекапи у `Assets/` | `Castle_Wall.png~`, `Spike.png~` | Видалено (не потрапляють у проєкт) |

---

## 7. Чек-лист перед додаванням нових файлів

- [ ] Файл розташований всередині `Assets/_Project/` у відповідній доменній підпапці.
- [ ] Назва написана англійською, без пробілів, спецсимволів і помилок.
- [ ] Асет має коректний префікс (`SPR_`, `ANIM_`, `AC_`, `SCN_`, `PFB_` тощо).
- [ ] C#-скрипт має точний збіг імені файлу з іменем класу та коректний `namespace Stitches.<Module>`.
- [ ] Додано відповідний `.meta`-файл (якщо файл переносився з іншого проєкту) для збереження GUID-посилань.
- [ ] Усі плейсхолдери помічені суфіксом `_Placeholder`.
