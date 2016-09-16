using System;
using UnityEngine;

namespace GPOSpeedFuelPump
{

	[KSPAddon(KSPAddon.Startup.FlightAndEditor, false)]
	public class GPOSpeedPumpController : MonoBehaviour
	{
		private float _lastUpdate;
		static private GPOSpeedPumpController instance;
		private readonly int _winId = new System.Random().Next(0x00010000, 0x7fffffff);
		private Rect _winPos = new Rect(Screen.width / 2, Screen.height / 2, 208, 16);
		private bool _winShow;
		private Part configPart = null;

		public static GPOSpeedPumpController Instance()
		{
			if (instance == null)
			{
				GPOprint("making new Controller instance");
				instance = new GPOSpeedPumpController();
			}
			return instance;
		}

		static public void GPOprint(string tacos)
		{
			print("[GPOSpeedFuelPump]: " + tacos); // tacos are awesome
		}

		private GPOSpeedPumpController()
		{
			GPOprint("Controller startup");
			instance = this;
		}

		/*		private void PumpOut(Part part, float secs)
				{
					foreach (PartResource pumpRes in part.Resources)
					{
						if (pumpRes.flowState)
						{ // don't operate if resource is locked
							if (GetResourceFlags(pumpRes.resourceName, 1) == 1)
							{
								foreach (Part shipPart in vessel.Parts)
								{
									float shipPartLevel = 0f;
									if (shipPart.Modules.Contains("GPOSpeedPump"))
									{
										var gpoSpeedPump = shipPart.Modules["GPOSpeedPump"] as GPOSpeedPump;
										if (gpoSpeedPump != null)
											shipPartLevel = gpoSpeedPump._pumpLevel;
									}
									if (shipPartLevel < _pumpLevel)
									{
										foreach (PartResource shipPartRes in shipPart.Resources)
										{
											if (shipPartRes.resourceName == pumpRes.resourceName)
											{
												if (shipPartRes.flowState)
												{ // don't operate if resource is locked
													double give = Math.Min(Math.Min(shipPartRes.maxAmount - shipPartRes.amount, pumpRes.amount), Math.Min(pumpRes.maxAmount, shipPartRes.maxAmount) / 10.0 * secs);
													if (give > 0.0)
													{ // Sanity check.  Apparently some other mods happily set amount or maxAmount to... interesting values...
														pumpRes.amount -= give;
														shipPartRes.amount += give;
													}
												}
											}
										}
									}
								}
							}
						}
					}
				}

				private void Balance(Part part)
				{
					foreach (PartResource pumpRes in part.Resources)
					{
						if (pumpRes.flowState)
						{ // don't operate if resource is locked
							if (GetResourceFlags(pumpRes.resourceName, 2) == 2)
							{
								double resAmt = 0f;
								double resMax = 0f;
								foreach (Part shipPart in vessel.Parts)
								{
									if (shipPart.Modules.Contains("GPOSpeedPump")
										&& ((GPOSpeedPump)shipPart.Modules["GPOSpeedPump"])._autoBalance
										&& Math.Abs(((GPOSpeedPump)shipPart.Modules["GPOSpeedPump"])._pumpLevel - _pumpLevel) < Tolerance)
									{
										foreach (PartResource shipPartRes in shipPart.Resources)
										{
											if (shipPartRes.resourceName == pumpRes.resourceName)
											{
												if (shipPartRes.flowState)
												{ // don't operate if resource is locked
													resAmt += shipPartRes.amount;
													resMax += shipPartRes.maxAmount;
												}
											}
										}
									}
								}
								foreach (Part shipPart in vessel.Parts)
								{
									if (shipPart.Modules.Contains("GPOSpeedPump")
										&& ((GPOSpeedPump)shipPart.Modules["GPOSpeedPump"])._autoBalance
										&& Math.Abs(((GPOSpeedPump)shipPart.Modules["GPOSpeedPump"])._pumpLevel - _pumpLevel) < Tolerance)
									{
										foreach (PartResource shipPartRes in shipPart.Resources)
										{
											if (shipPartRes.resourceName == pumpRes.resourceName)
											{
												if (shipPartRes.flowState)
												{ // don't operate if resource is locked
													shipPartRes.amount = shipPartRes.maxAmount * resAmt / resMax;
												}
											}
										}
									}
								}
							}
						}
					}
				}
		*/
		public void ConfigurePump(Part p)
		{
			GPOprint("Controller ConfigurePump()");
			if ((!_winShow) || (p!=configPart))
			{
				_winPos.xMin = Math.Min(Math.Max(0, p.vessel == null ? Screen.width - _winPos.width : Event.current.mousePosition.x + 180), Screen.width - _winPos.width);
				_winPos.yMin = Math.Min(Math.Max(0, Event.current.mousePosition.y), Screen.height - _winPos.height);
				_winPos.width = 208;
				_winPos.height = 16;
				_winShow = true;
				configPart = p;
			}
			else {
				_winShow = false;
				configPart = null;
			}
		}

		internal static Rect clampToScreen(Rect rect)
		{
			rect.x = Mathf.Clamp(rect.x, 0, Screen.width - rect.width);
			rect.y = Mathf.Clamp(rect.y, 0, Screen.height - rect.height);
			return rect;
		}

		private void OnGUI()
		{
			if (_winShow)
			{
				GUI.skin = null;
				_winPos = clampToScreen(GUILayout.Window(_winId, _winPos, DrawConfigWindow, "GPOSpeed Pump"));
			}
		}

		public void FixedUpdate()
		{
			/*
			if (_autoPump && _pumpLevel > 0f)
				PumpOut(Time.time - _lastUpdate);

			if (_autoBalance)
				Balance();*/

			_lastUpdate = Time.time;

		}

		public void Start()
		{
			GPOprint("Controller Start()");

			GPOprint("Controller hooking OnVesselSwitch");
			GameEvents.onVesselChange.Add(OnVesselSwitch);

			GPOprint("Advanced Tweakables = " + GameSettings.ADVANCED_TWEAKABLES.ToString());
			GameSettings.ADVANCED_TWEAKABLES = true;
			GPOprint("Advanced Tweakables = " + GameSettings.ADVANCED_TWEAKABLES.ToString());
		}

		public void OnVesselSwitch(Vessel v)
		{
			GPOprint("Controller OnVesselSwitch()");
			_winShow = false;
		}

		private void DrawConfigWindow(int id)
		{
			GUIStyle style = new GUIStyle(GUI.skin.button) { padding = new RectOffset(8, 8, 4, 4) };

			GUILayout.BeginVertical();
			GUILayout.Label(configPart.partInfo.title);
			GPOSpeedPump pm = (GPOSpeedPump)configPart.Modules["GPOSpeedPump"];
			foreach (PartResource pr in configPart.Resources)
			{
				pm.SetResourceFlags(pr.resourceName, pm.GetResourceFlags(pr.resourceName, ~1) | (GUILayout.Toggle(pm.GetResourceFlags(pr.resourceName, 1) == 1, "Pump " + pr.resourceName) ? 1 : 0));
				pm.SetResourceFlags(pr.resourceName, pm.GetResourceFlags(pr.resourceName, ~2) | (GUILayout.Toggle(pm.GetResourceFlags(pr.resourceName, 2) == 2, "Balance " + pr.resourceName) ? 2 : 0));
			}
			if (GUILayout.Button("Close", style, GUILayout.ExpandWidth(true)))
			{
				_winShow = false;
			}
			GUILayout.EndVertical();

			GUI.DragWindow();
		}


	}
}
