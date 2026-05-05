# S&box FPS Weapon System

---
> 🇬🇧 [English version](#english-version) | 🇷🇺 [Русская версия](#русская-версия) 
---

<img width="2136" height="1080" alt="Без имени-1" src="https://github.com/user-attachments/assets/b3bec65d-0c11-47e3-959e-a5e2193005c1" />

## English Version

### Update:

I didn't edit the description; I'm too lazy.

- Added - Gunshot sounds for the M4a1.
- Added - Switching weapons/fists/empty to keys 1-3.
- Added new code - WeaponSwitcher connected to the WeaponModel hierarchy, which is used to switch.

### Description
A basic first-person shooting system for S&box built on the Scene/Component system.
Designed as a starting point for FPS games.

### Features
- Automatic and semi-automatic firing modes
- Hitscan bullet tracing (`Scene.Trace.Ray`)
- Muzzle flash attached to the model's `muzzle` bone
- Bullet impact effect at the point of contact
- Physics impulse on objects with `Rigidbody`
- Ammo system with reload animation
- All parameters configurable via the Inspector

---

### Component: Shooting.cs
The main shooting component. Attach it to the **WeaponModel** object.

| Field | Type | Description |
|---|---|---|
| WeaponModel | SkinnedModelRenderer | Weapon model renderer (`v_m4a1`) |
| WeaponRenderer | SkinnedModelRenderer | Same renderer, used to get the `muzzle` bone position |
| EyePoint | GameObject | Camera object — ray origin |
| ShootSound | SoundEvent | Gunshot sound |
| DryFireSound | SoundEvent | Empty magazine sound |
| FireRate | float | Shots per second (0–30) |
| Damage | float | Damage per shot |
| BulletForce | float | Impulse force on physics objects |
| BulletSize | float | Bullet trace radius |
| BulletRange | float | Bullet range |
| Spread | float | Bullet spread (0 = no spread) |
| IsAutomatic | bool | Automatic or semi-automatic fire |
| MaxAmmo | int | Magazine size |
| ReloadTime | float | Reload time in seconds |
| BulletImpactEffect | PrefabFile | Impact effect prefab (`impact.generic`) |
| MuzzleFlashEffect | PrefabFile | Muzzle flash prefab (`rifle_muzzleflash`) |

---

### Recommended Settings for M4A1
```
FireRate     = 10
Damage       = 25
BulletForce  = 200
BulletRange  = 5000
Spread       = 0.05
IsAutomatic  = true
MaxAmmo      = 30
ReloadTime   = 2
```

---

### Scene Hierarchy
```
Camera
  └─ WeaponModel          ← Shooting component goes here
       ├─ v_m4a1          ← SkinnedModelRenderer (WeaponModel + WeaponRenderer)
       └─ v_first_person_arms_human
```

---

### Scene Object Requirements

**Weapon:**
- Model must have a `muzzle` bone — muzzle flash is attached to it
- Model must have `b_attack` and `b_reload` animations

**Physics objects (targets):**
- `Rigidbody` component required
- `Mass Override` > 0 (e.g. `1`)
- `Start Asleep` = disabled

**Player:**
- Tag `player` on the PlayerController object — so the ray does not hit itself

---

### Effect Prefabs
Taken from the built-in S&box Asset Browser:
- `particles/muzzleflash/rifle_muzzleflash` — muzzle flash
- `particles/muzzleflash/impact.generic` — bullet impact effect

---

### Full Code: Shooting.cs
```csharp
using Sandbox;

public sealed class Shooting : Component
{
    [Property] public SkinnedModelRenderer WeaponModel { get; set; }
    [Property] public SkinnedModelRenderer WeaponRenderer { get; set; }
    [Property] public GameObject EyePoint { get; set; }

    [Property] public SoundEvent ShootSound { get; set; }
    [Property] public SoundEvent DryFireSound { get; set; }

    [Property, Range( 0f, 30f )] public float FireRate { get; set; } = 10f;
    [Property] public float Damage { get; set; } = 25f;
    [Property] public float BulletForce { get; set; } = 200f;
    [Property] public float BulletSize { get; set; } = 1f;
    [Property] public float BulletRange { get; set; } = 5000f;
    [Property] public float Spread { get; set; } = 0.05f;
    [Property] public bool IsAutomatic { get; set; } = true;

    [Property] public int MaxAmmo { get; set; } = 30;
    [Property] public float ReloadTime { get; set; } = 2.0f;

    [Property] public PrefabFile BulletImpactEffect { get; set; }
    [Property] public PrefabFile MuzzleFlashEffect { get; set; }

    private TimeSince _timeSinceShot;
    private int _currentAmmo;
    private bool _isReloading = false;

    protected override void OnStart()
    {
        _currentAmmo = MaxAmmo;
    }

    protected override void OnUpdate()
    {
        bool wantsShoot = IsAutomatic
            ? Input.Down( "attack1" )
            : Input.Pressed( "attack1" );

        if ( wantsShoot )
            TryShoot();

        if ( Input.Pressed( "reload" ) && !_isReloading )
            _ = Reload();
    }

    private void TryShoot()
    {
        if ( _timeSinceShot < 1f / FireRate )
            return;

        if ( _currentAmmo <= 0 )
        {
            if ( !_isReloading ) _ = Reload();
            return;
        }

        if ( _isReloading )
            return;

        _timeSinceShot = 0;
        _currentAmmo--;

        WeaponModel?.Set( "b_attack", true );

        if ( MuzzleFlashEffect is not null && WeaponRenderer is not null )
        {
            var prefabScene = SceneUtility.GetPrefabScene( MuzzleFlashEffect );
            if ( prefabScene is not null )
            {
                if ( WeaponRenderer.TryGetBoneTransform( "muzzle", out var boneTransform ) )
                {
                    var fx = prefabScene.Clone( new Transform( boneTransform.Position, Scene.Camera.WorldRotation ) );
                    _ = DestroyAfter( fx, 0.15f );
                }
            }
        }

        ShootBullet();
    }

    private void ShootBullet()
    {
        var startPos = EyePoint?.WorldPosition ?? Scene.Camera?.WorldPosition ?? WorldPosition;
        var dir = EyePoint?.WorldRotation.Forward ?? Scene.Camera?.WorldRotation.Forward ?? WorldRotation.Forward;

        dir += new Vector3(
            Game.Random.Float( -Spread, Spread ),
            Game.Random.Float( -Spread, Spread ),
            Game.Random.Float( -Spread, Spread )
        );
        dir = dir.Normal;

        var tr = Scene.Trace.Ray( startPos, startPos + dir * BulletRange ).Size( BulletSize ).WithoutTags( "trigger", "player" ).UseHitboxes( true ).Run();

        if ( tr.Hit )
        {
            if ( BulletImpactEffect is not null )
            {
                var prefabScene = SceneUtility.GetPrefabScene( BulletImpactEffect );
                if ( prefabScene is not null )
                {
                    var impact = prefabScene.Clone( new Transform( tr.HitPosition, Rotation.LookAt( tr.Normal ) ) );
                    _ = DestroyAfter( impact, 1f );
                }
            }

            if ( tr.GameObject is not null )
            {
                var rb = tr.GameObject.Components.Get<Rigidbody>();
                if ( rb is not null )
                {
                    rb.ApplyImpulseAt( tr.HitPosition, dir * BulletForce );
                }
            }
        }
    }

    private async Task Reload()
    {
        if ( _currentAmmo == MaxAmmo ) return;
        _isReloading = true;

        WeaponModel?.Set( "b_reload", true );

        await Task.DelaySeconds( ReloadTime );

        _currentAmmo = MaxAmmo;
        _isReloading = false;
    }

    private async Task DestroyAfter( GameObject go, float seconds )
    {
        await Task.DelaySeconds( seconds );
        if ( go.IsValid() )
            go.Destroy();
    }
}
```

---
---

## Русская Версия

### Обновление:

Описание не исправлял, мне лень.

- Добавлены - Звуки выстрелов для M4a1.
- Добавлены - Смена оружие/кулаки/пусто на клавиши 1-3. 
- Добавлен новый код - WeaponSwitcher подключенный к иерархии WeaponModel, с помощью которого происходит смена.


### Описание
Базовая система стрельбы от первого лица для S&box на основе компонентной системы (Scene System). Подходит как стартовая точка для FPS игр.

### Возможности
- Автоматический и полуавтоматический режимы стрельбы
- Hitscan трассировка луча (`Scene.Trace.Ray`)
- Вспышка дула привязанная к кости `muzzle` модели
- Эффект попадания в точке контакта
- Физический импульс на объекты с `Rigidbody`
- Система патронов и перезарядка с анимацией
- Все параметры настраиваются через инспектор

---

### Компонент: Shooting.cs
Основной компонент стрельбы. Вешается на объект **WeaponModel**.

| Поле | Тип | Описание |
|---|---|---|
| WeaponModel | SkinnedModelRenderer | Рендерер модели оружия (`v_m4a1`) |
| WeaponRenderer | SkinnedModelRenderer | Тот же рендерер, используется для получения позиции кости `muzzle` |
| EyePoint | GameObject | Объект камеры — откуда летит луч |
| ShootSound | SoundEvent | Звук выстрела |
| DryFireSound | SoundEvent | Звук пустого магазина |
| FireRate | float | Выстрелов в секунду (0–30) |
| Damage | float | Урон за выстрел |
| BulletForce | float | Сила импульса на физические объекты |
| BulletSize | float | Радиус трассировки пули |
| BulletRange | float | Дальность стрельбы |
| Spread | float | Разброс пуль (0 = нет разброса) |
| IsAutomatic | bool | Автоматический или одиночный огонь |
| MaxAmmo | int | Размер магазина |
| ReloadTime | float | Время перезарядки в секундах |
| BulletImpactEffect | PrefabFile | Префаб эффекта попадания (`impact.generic`) |
| MuzzleFlashEffect | PrefabFile | Префаб вспышки дула (`rifle_muzzleflash`) |

---

### Настройки для M4A1
```
FireRate     = 10
Damage       = 25
BulletForce  = 200
BulletRange  = 5000
Spread       = 0.05
IsAutomatic  = true
MaxAmmo      = 30
ReloadTime   = 2
```

---

### Иерархия сцены
```
Camera
  └─ WeaponModel          ← компонент Shooting здесь
       ├─ v_m4a1          ← SkinnedModelRenderer (WeaponModel + WeaponRenderer)
       └─ v_first_person_arms_human
```

---

### Требования к объектам сцены

**Оружие:**
- Модель должна иметь кость `muzzle` — к ней привязывается вспышка
- Модель должна иметь анимации `b_attack` и `b_reload`

**Физические объекты (цели):**
- Компонент `Rigidbody`
- `Mass Override` > 0 (например `1`)
- `Start Asleep` = выключено

**Игрок:**
- Тег `player` на объекте PlayerController — чтобы луч не попадал в себя

---

### Префабы эффектов
Берутся из встроенного Asset Browser S&box:
- `particles/muzzleflash/rifle_muzzleflash` — вспышка дула
- `particles/muzzleflash/impact.generic` — эффект попадания

- <img width="343" height="836" alt="image" src="https://github.com/user-attachments/assets/b067c229-78c6-46af-8cbd-a7462f6baec3" />

