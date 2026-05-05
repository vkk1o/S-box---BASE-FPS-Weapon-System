using Sandbox;
using System.Threading.Tasks;

public sealed class WeaponSwitcher : Component
{
	[Property] public GameObject WeaponM4A1 { get; set; }
	[Property] public Shooting ShootingComponent { get; set; }
	[Property] public SkinnedModelRenderer ArmsRenderer { get; set; }

	protected override void OnUpdate()
	{
		if ( Input.Pressed( "slot1" ) )
			SwitchTo( 1 );
		else if ( Input.Pressed( "slot2" ) )
			SwitchTo( 2 );
		else if ( Input.Pressed( "slot3" ) )
			SwitchTo( 3 );
	}

	private void SwitchTo( int slot )
	{
		switch ( slot )
		{
			case 1: // M4A1
				WeaponM4A1.Enabled = true;
				ShootingComponent.Enabled = true;
				ArmsRenderer.GameObject.Enabled = true;
				break;

			case 2: // Кулаки
				WeaponM4A1.Enabled = false;
				ShootingComponent.Enabled = false;
				ArmsRenderer.GameObject.Enabled = false;
				_ = ShowArmsAfter();
				break;

			case 3: // Пусто
				WeaponM4A1.Enabled = false;
				ShootingComponent.Enabled = false;
				ArmsRenderer.GameObject.Enabled = false;
				break;
		}
	}

	private async Task ShowArmsAfter()
	{
		await Task.DelaySeconds( 0.05f );
		ArmsRenderer.GameObject.Enabled = true;
	}
}
