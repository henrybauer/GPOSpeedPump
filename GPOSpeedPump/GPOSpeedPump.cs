/* Goodspeed Automatic Fuel Pump (c) Copyright 2014 Gaius Goodspeed

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.
 * 
 * 
 * 30/04/2015 - Taken over by GPO for KSP 1.0
 * 24/01/2016 - updated for 1.0.5.  Respect resource lock, and don't operate on NO_FLOW resources.

http://www.gnu.org/licenses/gpl.html
*/

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GPOSpeedFuelPump
{
	public class GPOSpeedPump : PartModule
	{
		private readonly int _winId = new System.Random ().Next (0x00010000, 0x7fffffff);
		private Rect _winPos = new Rect (Screen.width / 2, Screen.height / 2, 208, 16);
		private bool _winShow;
		private float _lastUpdate;
		private const float Tolerance = (float)0.0001;
        
		private Dictionary<string, int> _resourceFlags;

		[KSPField (isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Pump Level"), UI_FloatRange (minValue = 0f, maxValue = 16f, stepIncrement = 1f)]
		public float _pumpLevel;
        
		[KSPField (isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Pump is "), UI_Toggle (disabledText = "Off", enabledText = "On")]
		public bool _autoPump;
      
		[KSPField (isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Balance"), UI_Toggle (disabledText = "No", enabledText = "Yes")]
		public bool _autoBalance;

		private int GetResourceFlags (string resourceName, int mask)
		{
			try {
			if (_resourceFlags == null)
				_resourceFlags = new Dictionary<string, int> ();
		
			if (!_resourceFlags.ContainsKey (resourceName)) {
				string cfgValue = GameDatabase.Instance.GetConfigs ("PART").Single (c => c.name.Replace ('_', '.') == part.partInfo.name)
                                              .config.GetNodes ("MODULE").Single (n => n.GetValue ("name") == moduleName).GetValue (resourceName + "Flags");
				if (!String.IsNullOrEmpty (cfgValue)) {
					int flags;
					if (Int32.TryParse (cfgValue, out flags))
						_resourceFlags.Add (resourceName, flags);
					else
						SetResourceFlags (resourceName, -1);
				} else
					SetResourceFlags (resourceName, -1);
			}
		
			return _resourceFlags [resourceName] & mask;
			} catch (Exception e) {
				return 0;
			}
		}

		private void SetResourceFlags (string resourceName, int value)
		{
			if (_resourceFlags == null)
				_resourceFlags = new Dictionary<string, int> ();

			if (isFlowableResource (resourceName)) {
				_resourceFlags [resourceName] = value;
			} else { // don't operate on NO_FLOW resources like SolidFuel
				_resourceFlags [resourceName] = 0;
			}
		}

		public override void OnLoad (ConfigNode cn)
		{
			// KSP is booting up
			if (part.partInfo == null)
				return;
		
			foreach (PartResource pr in part.Resources) {
				string cfgValue = cn.GetValue (pr.resourceName + "Flags");
				if (!String.IsNullOrEmpty (cfgValue)) {
					int flags;
					if (Int32.TryParse (cfgValue, out flags))
						SetResourceFlags (pr.resourceName, flags);
				}
			}
		}

		public override void OnSave (ConfigNode cn)
		{
			if (_resourceFlags == null)
				return;
		
			foreach (var rf in _resourceFlags) {
				if (rf.Value != -1) {
					string flagName = rf.Key + "Flags";
					if (cn.HasValue (flagName))
						cn.SetValue (flagName, (rf.Value & 3).ToString ());
					else
						cn.AddValue (flagName, (rf.Value & 3).ToString ());
				}
			}
		}

		private bool isFlowableResource (string resourceName)
		{
			PartResourceDefinition resource = PartResourceLibrary.Instance.GetDefinition (resourceName);
			if (resource == null) {
				return false;
			}
			if (resource.resourceFlowMode == ResourceFlowMode.NO_FLOW) {
				return false;
			}
			if (resource.resourceFlowMode == ResourceFlowMode.NULL) {
				return false;
			}
			return true;
		}

		private void DrawConfigWindow (int id)
		{
			GUIStyle style = new GUIStyle (GUI.skin.button) { padding = new RectOffset (8, 8, 4, 4) };

			GUILayout.BeginVertical ();
			GUILayout.Label (part.partInfo.title);
			foreach (PartResource pr in part.Resources) {
				SetResourceFlags (pr.resourceName, GetResourceFlags (pr.resourceName, ~1) | (GUILayout.Toggle (GetResourceFlags (pr.resourceName, 1) == 1, "Pump " + pr.resourceName) ? 1 : 0));
				SetResourceFlags (pr.resourceName, GetResourceFlags (pr.resourceName, ~2) | (GUILayout.Toggle (GetResourceFlags (pr.resourceName, 2) == 2, "Balance " + pr.resourceName) ? 2 : 0));
			}
			if (GUILayout.Button ("Close", style, GUILayout.ExpandWidth (true))) {
				_winShow = false;
			}
			GUILayout.EndVertical ();
		
			GUI.DragWindow ();
		}

		internal static Rect clampToScreen (Rect rect)
		{
			rect.x = Mathf.Clamp (rect.x, 0, Screen.width - rect.width);
			rect.y = Mathf.Clamp (rect.y, 0, Screen.height - rect.height);
			return rect;
		}

		private void OnGUI ()
		{
			if (_winShow) {
				GUI.skin = null;
				_winPos = clampToScreen (GUILayout.Window (_winId, _winPos, DrawConfigWindow, "GPOSpeed Pump"));
			}
		}

		[KSPEvent (guiActive = true, guiActiveEditor = true, guiName = "Pump Options")]
		public void ConfigurePump ()
		{
			if (!_winShow) {
				_winPos.xMin = Math.Min (Math.Max (0, vessel == null ? Screen.width - _winPos.width : Event.current.mousePosition.x + 180), Screen.width - _winPos.width);
				_winPos.yMin = Math.Min (Math.Max (0, Event.current.mousePosition.y), Screen.height - _winPos.height);
				_winPos.width = 208;
				_winPos.height = 16;
				_winShow = true;
			} else {
				_winShow = false;
			}
		}

		private void PumpOut (float secs)
		{
			foreach (PartResource pumpRes in part.Resources) {
				if (pumpRes.flowState) { // don't operate if resource is locked
					if (GetResourceFlags (pumpRes.resourceName, 1) == 1) {
						foreach (Part shipPart in vessel.Parts) {
							float shipPartLevel = 0f;
							if (shipPart.Modules.Contains ("GPOSpeedPump")) {
								var gpoSpeedPump = shipPart.Modules ["GPOSpeedPump"] as GPOSpeedPump;
								if (gpoSpeedPump != null)
									shipPartLevel = gpoSpeedPump._pumpLevel;
							}
							if (shipPartLevel < _pumpLevel) {
								foreach (PartResource shipPartRes in shipPart.Resources) {
									if (shipPartRes.resourceName == pumpRes.resourceName) {
										if (shipPartRes.flowState) { // don't operate if resource is locked
											double give = Math.Min (Math.Min (shipPartRes.maxAmount - shipPartRes.amount, pumpRes.amount), Math.Min (pumpRes.maxAmount, shipPartRes.maxAmount) / 10.0 * secs);
											if (give > 0.0) { // Sanity check.  Apparently some other mods happily set amount or maxAmount to... interesting values...
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

		private void Balance ()
		{
			foreach (PartResource pumpRes in part.Resources) {
				if (pumpRes.flowState) { // don't operate if resource is locked
					if (GetResourceFlags (pumpRes.resourceName, 2) == 2) {
						double resAmt = 0f;
						double resMax = 0f;
						foreach (Part shipPart in vessel.Parts) {
							if (shipPart.Modules.Contains ("GPOSpeedPump")
							    && ((GPOSpeedPump)shipPart.Modules ["GPOSpeedPump"])._autoBalance
							    && Math.Abs (((GPOSpeedPump)shipPart.Modules ["GPOSpeedPump"])._pumpLevel - _pumpLevel) < Tolerance) {
								foreach (PartResource shipPartRes in shipPart.Resources) {
									if (shipPartRes.resourceName == pumpRes.resourceName) {
										if (shipPartRes.flowState) { // don't operate if resource is locked
											resAmt += shipPartRes.amount;
											resMax += shipPartRes.maxAmount;
										}
									}
								}
							}
						}
						foreach (Part shipPart in vessel.Parts) {
							if (shipPart.Modules.Contains ("GPOSpeedPump")
							    && ((GPOSpeedPump)shipPart.Modules ["GPOSpeedPump"])._autoBalance
							    && Math.Abs (((GPOSpeedPump)shipPart.Modules ["GPOSpeedPump"])._pumpLevel - _pumpLevel) < Tolerance) {
								foreach (PartResource shipPartRes in shipPart.Resources) {
									if (shipPartRes.resourceName == pumpRes.resourceName) {
										if (shipPartRes.flowState) { // don't operate if resource is locked
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

		public override void OnUpdate ()
		{
			float now = Time.time;
		
			if (_autoPump && _pumpLevel > 0f)
				PumpOut (now - _lastUpdate);
		
			if (_autoBalance)
				Balance ();
		
			_lastUpdate = now;
		
			if (_winShow && !vessel.isActiveVessel) {
				_winShow = false;
			}
		}
	}
}
