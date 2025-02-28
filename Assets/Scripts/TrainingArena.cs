using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using ArenaBuilders;
using UnityEngineExtensions;
using ArenasParameters;
using Holders;
using Random = UnityEngine.Random;

public class TrainingArena : MonoBehaviour
{
	public ListOfPrefabs prefabs = new ListOfPrefabs();
	public GameObject spawnedObjectsHolder;
	public int maxSpawnAttemptsForAgent = 100;
	public int maxSpawnAttemptsForPrefabs = 20;
	public ListOfBlackScreens blackScreens = new ListOfBlackScreens();

	[HideInInspector]
	public int arenaID = -1;

	[HideInInspector]
	public int maxarenaID = -1;

	[HideInInspector]
	public TrainingAgent _agent;
	private ArenaBuilder _builder;
	public ArenaBuilder Builder
	{
		get { return _builder; }
	}
	private ArenaConfiguration _arenaConfiguration = new ArenaConfiguration();
	private AAI3EnvironmentManager _environmentManager;
	private List<Fade> _fades = new List<Fade>();
	private bool _lightStatus = true;
	private int _agentDecisionInterval; // How many frames between decisions, reads from agent's decision requester
	private bool _firstReset = true;
	private List<GameObject> spawnedRewards = new List<GameObject>();
	public bool showNotification { get; set; }

	internal void Awake()
	{
		_builder = new ArenaBuilder(
			gameObject,
			spawnedObjectsHolder,
			maxSpawnAttemptsForPrefabs,
			maxSpawnAttemptsForAgent
		);
		_environmentManager = GameObject.FindObjectOfType<AAI3EnvironmentManager>();
		_agent = FindObjectsOfType<TrainingAgent>(true)[0];
		_agentDecisionInterval = _agent.GetComponentInChildren<DecisionRequester>().DecisionPeriod;
		_fades = blackScreens.GetFades();

		// Subscribe to the reward spawn event from spawner_InteractiveButton.cs
		Spawner_InteractiveButton.RewardSpawned += OnRewardSpawned;
	}

	private void OnDestroy()
	{
		// Unsubscribe when the object is destroyed to avoid memory leaks
		Spawner_InteractiveButton.RewardSpawned -= OnRewardSpawned;
	}

	private void OnRewardSpawned(GameObject reward)
	{
		spawnedRewards.Add(reward);
	}

	public void ResetArena()
	{
		Debug.Log("Resetting Arena");
		foreach (GameObject holder in transform.FindChildrenWithTag("spawnedObjects"))
		{
			holder.SetActive(false);
			Destroy(holder);
		}

		int totalArenas = _environmentManager.getMaxArenaID();

		if (_firstReset)
		{
			if (_environmentManager.GetRandomizeArenasStatus())
			{
				arenaID = Random.Range(0, totalArenas + 1);
			}
			else
			{
				arenaID = 0; // On the first reset, set the arenaID to 0 if not randomizing.
			}
			_firstReset = false;
		}
		else
		{
			if (_environmentManager.GetRandomizeArenasStatus())
			{
				List<int> validArenaIDs = new List<int>();
				for (int i = 0; i <= totalArenas; i++)
				{
					if (i != arenaID && _environmentManager.GetConfiguration(i, out _))
					{
						validArenaIDs.Add(i);
					}
				}

				if (validArenaIDs.Count > 0)
				{
					arenaID = validArenaIDs[Random.Range(0, validArenaIDs.Count)];
				}
				else
				{
					Debug.LogWarning("No valid arena configurations found other than current. Keeping the same arena.");
				}
			}
			else
			{
				// Sequentially select the next arena
				do
				{
					arenaID = (arenaID + 1) % (totalArenas + 1);
				}
				while (!_environmentManager.GetConfiguration(arenaID, out _));
			}
		}

		ArenaConfiguration newConfiguration;
		if (!_environmentManager.GetConfiguration(arenaID, out newConfiguration))
		{
			Debug.LogWarning($"Failed to retrieve configuration for arenaID: {arenaID}. Looping back to the first arena.");
			arenaID = 0;
			if (!_environmentManager.GetConfiguration(arenaID, out newConfiguration))
			{
				Debug.LogError($"Critical error: Failed to retrieve configuration for default arenaID: {arenaID}");
				return;
			}
		}
		_arenaConfiguration = newConfiguration;

		// Pass the showNotification attribute to the agent
		_agent.showNotification = _arenaConfiguration.showNotification;

		Debug.Log("Updating");
		_arenaConfiguration.SetGameObject(prefabs.GetList());
		_builder.Spawnables = _arenaConfiguration.spawnables;
		_arenaConfiguration.toUpdate = false;
		_agent.MaxStep = 0; // We never time the environment out unless agent health goes to 0
		_agent.timeLimit = _arenaConfiguration.T * _agentDecisionInterval;
		_builder.Build();
		_arenaConfiguration.lightsSwitch.Reset();

		if (_arenaConfiguration.randomSeed != 0)
		{
			Random.InitState(_arenaConfiguration.randomSeed);
		}

		foreach (var reward in spawnedRewards)
		{
			Destroy(reward);
		}
		spawnedRewards.Clear();
	}

	public void UpdateLigthStatus()
	{
		int stepCount = _agent.StepCount;
		bool newLight = _arenaConfiguration.lightsSwitch.LightStatus(
			stepCount,
			_agentDecisionInterval
		);
		if (newLight != _lightStatus)
		{
			_lightStatus = newLight;
			foreach (Fade fade in _fades)
			{
				fade.StartFade();
			}
		}
	}

	void FixedUpdate()
	{
		UpdateLigthStatus();
	}

	public ArenaConfiguration ArenaConfig
	{
		get { return _arenaConfiguration; }
	}
}
