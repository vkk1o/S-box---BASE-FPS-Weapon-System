using Sandbox;
using System.Threading.Tasks;

public sealed class Shooting : Component
{
	[Property] public SkinnedModelRenderer WeaponModel { get; set; }
	[Property] public SkinnedModelRenderer WeaponRenderer { get; set; }
	[Property] public GameObject EyePoint { get; set; }
	[Property] public SoundEvent ShootSound { get; set; }
	[Property] public SoundEvent DryFireSound { get; set; }

	[Property, Range( 0f, 30f )] public float FireRate { get; set; } = 10f;
	[Property] public float Damage { get; set; } = 25f;
	[Property] public float BulletForce { get; set; } = 100f;
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
					Log.Info( $"BonePos={boneTransform.Position}" );
					var fx = prefabScene.Clone( new Transform( boneTransform.Position, Scene.Camera.WorldRotation ) );
					_ = DestroyAfter( fx, 0.15f );
				}
				else
				{
					Log.Info( "Кость muzzle не найдена!" );
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

		Log.Info( $"Hit={tr.Hit}, Pos={tr.HitPosition}, Obj={tr.GameObject?.Name}" );

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
