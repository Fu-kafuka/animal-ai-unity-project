﻿using System.Collections.Generic;
using System.Reflection;
using System;
using System.Linq;
using UnityEngine;
using UnityEngineExtensions;
using Holders;
using PrefabInterface;
using ArenasParameters;

namespace ArenaBuilders
{
    /// <summary>
    /// An ArenaBuilder is attached to each instantiated arena. Each time the arena is reset the 
    /// Builder takes a list of items to spawn in the form of a list of Spawnable items, and 
    /// attempts to instantiate these in the arena. For each GameObject instantiated at a specific
    /// position, the builder will check if any object is already present at this position. If not
    /// the object is then moved to the desired position, if the position is occupied the object is
    /// destroyed and therefore ignored. Positions, rotations and scales can be passed by the 
    /// user or randomized. In case they are randomized the builder will attempt to spawn items
    /// a certain number of times before giving up and moving on to the next object if no free space was found.
    /// </summary>
    public class ArenaBuilder
    {

        /// Range of values X and Y can take (basically the size of the arena)
        private float _rangeX;
        private float _rangeZ;

        /// The arena we're in
        private Transform _arena;

        /// Empty gameobject that will hold all instantiated GameObjects
        private GameObject _spawnedObjectsHolder;

        /// Maximum number of attempts to spawn the objects and the agent
        private int _maxSpawnAttemptsForPrefabs;
        private int _maxSpawnAttemptsForAgent;

        /// Agent and its components
        private GameObject _agent;
        private Collider _agentCollider;
        private Rigidbody _agentRigidbody;

        /// List of good goals that have been instantiated, used to set numberOfGoals in these goals
        private List<Goal> _goodGoalsMultiSpawned;
        public void AddToGoodGoalsMultiSpawned(Goal ggm) { _goodGoalsMultiSpawned.Add(ggm); updateGoodGoalsMulti(); }
        public void AddToGoodGoalsMultiSpawned(GameObject ggm) { _goodGoalsMultiSpawned.Add(ggm.GetComponent<Goal>()); updateGoodGoalsMulti(); }

        /// Buffer to allow space around instantiated objects
        // public Vector3 safeSpawnBuffer = Vector3.zero;

        /// The list of Spawnables the ArenaBuilder will attempt to spawn at each reset
        [HideInInspector]
        public List<Spawnable> Spawnables { get; set; }


        public ArenaBuilder(GameObject arenaGameObject, GameObject spawnedObjectsHolder,
                            int maxSpawnAttemptsForPrefabs, int maxSpawnAttemptsForAgent)
        {
            _arena = arenaGameObject.GetComponent<Transform>();
            Transform spawnArenaTransform = _arena
                                            .FindChildWithTag("spawnArena")
                                            .GetComponent<Transform>();
            _rangeX = spawnArenaTransform.localScale.x;
            _rangeZ = spawnArenaTransform.localScale.z;
            _agent = _arena.Find("AAI3Agent").Find("Agent").gameObject;;
            _agentCollider = _agent.GetComponent<Collider>();
            _agentRigidbody = _agent.GetComponent<Rigidbody>();
            _spawnedObjectsHolder = spawnedObjectsHolder;
            _maxSpawnAttemptsForPrefabs = maxSpawnAttemptsForPrefabs;
            _maxSpawnAttemptsForAgent = maxSpawnAttemptsForAgent;
            Spawnables = new List<Spawnable>();
            _goodGoalsMultiSpawned = new List<Goal>();
        }

        public void Build()
        {
            _goodGoalsMultiSpawned.Clear();

            GameObject spawnedObjectsHolder = GameObject.Instantiate(_spawnedObjectsHolder,
                                                                     _arena.transform,
                                                                     false);
            spawnedObjectsHolder.transform.parent = _arena;

            InstantiateSpawnables(spawnedObjectsHolder);

            updateGoodGoalsMulti();
        }

        private void InstantiateSpawnables(GameObject spawnedObjectsHolder)
        {

            Debug.Log("Spawnables has " + Spawnables.Capacity + " entries");
            List<Spawnable> agentSpawnablesFromUser = Spawnables.Where(x => x.gameObject != null
                                                                && x.gameObject.CompareTag("agent"))
                                                                .ToList();

            // If we are provided with an agent's caracteristics we want to instantiate it first and
            // then ignore any item spawning at the same spot. Otherwise we spawn it last to allow 
            // more objects to spawn
            if (agentSpawnablesFromUser.Any())
            {
                _agentCollider.enabled = false;
                SpawnAgent(agentSpawnablesFromUser[0]);
                _agentCollider.enabled = true;
                SpawnObjects(spawnedObjectsHolder);
            }
            else
            {
                _agentCollider.enabled = false;
                SpawnObjects(spawnedObjectsHolder);
                SpawnAgent(null);
                _agentCollider.enabled = true;
            }
        }

        private void SpawnObjects(GameObject spawnedObjectsHolder)
        {
            foreach (Spawnable spawnable in Spawnables)
            {
                if (spawnable.gameObject != null)
                {
                    if (!spawnable.gameObject.CompareTag("agent"))
                    {
                        InstantiateSpawnable(spawnable, spawnedObjectsHolder);
                    }
                }
            }
        }

        private void InstantiateSpawnable(Spawnable spawnable, GameObject spawnedObjectsHolder)
        {
            List<Vector3> positions = spawnable.positions;
            List<float> rotations = spawnable.rotations;
            List<Vector3> sizes = spawnable.sizes;
            List<Vector3> colors = spawnable.colors;
            // ======== EXTRA/OPTIONAL PARAMETERS ========
            // use for SignPosterboard symbols, Decay/SizeChange rates, Dispenser settings, etc.
            List<string> symbolNames = spawnable.symbolNames;
            List<float> delays = spawnable.delays;
            List<float> initialValues = spawnable.initialValues;
            List<float> finalValues = spawnable.finalValues;
            List<float> changeRates = spawnable.changeRates;

            int numberOfPositions = positions.Count;
            int numberOfRotations = rotations.Count;
            int numberOfSizes = sizes.Count;
            int numberOfColors = colors.Count;
            int numberOfSymbolNames = optionalCount(symbolNames);
            int numberOfDelays = optionalCount(delays);
            int numberOfInitialValues = optionalCount(initialValues);
            int numberOfFinalValues = optionalCount(finalValues);
            int numberOfChangeRates = optionalCount(changeRates);

            int n = new int[] {
                numberOfPositions, numberOfRotations,
                numberOfSizes, numberOfColors,
                numberOfSymbolNames, numberOfDelays,
                numberOfInitialValues, numberOfFinalValues,
                numberOfChangeRates
            }.Max();

            int k = 0;
            do
            {
                GameObject gameObjectInstance = GameObject.Instantiate(spawnable.gameObject,
                                                                spawnedObjectsHolder.transform,
                                                                false);
                gameObjectInstance.SetLayer(1);
                Vector3 position = k < numberOfPositions ? positions[k] : -Vector3.one;
                float rotation = k < numberOfRotations ? rotations[k] : -1;
                Vector3 size = k < numberOfSizes ? sizes[k] : -Vector3.one;
                Vector3 color = k < numberOfColors ? colors[k] : -Vector3.one;
                // for optional parameters, use default values
                // @TO-DO default values should be stored somewhere more obvious and global !
                string symbolName = k < numberOfSymbolNames ? symbolNames[k] : null;
                float delay = k < numberOfDelays ? delays[k] : 0;
                bool ripen_or_grow = (spawnable.name.StartsWith("Anti") || spawnable.name.StartsWith("Grow"));
                float initialValue = k < numberOfInitialValues ? initialValues[k] : (ripen_or_grow?0.5f:2.5f);
                float finalValue = k < numberOfFinalValues ? finalValues[k] : (ripen_or_grow?2.5f:0.5f);
                float changeRate = k < numberOfChangeRates ? changeRates[k] : -0.005f;
                // group together in dictionary so can pass as one argument to Spawner
                // (means we won't have to keep updating the arguments of Spawner function
                // each time we add to optional parameters)
                Dictionary<string, object> optionals = new Dictionary<string, object>() {
                    {nameof(symbolName), symbolName}, {nameof(delay), delay},
                    {nameof(initialValue), initialValue}, {nameof(finalValue), finalValue},
                    {nameof(changeRate), changeRate}
                };

                PositionRotation spawnPosRot = SamplePositionRotation(gameObjectInstance,
                                                                    _maxSpawnAttemptsForPrefabs,
                                                                    position,
                                                                    rotation,
                                                                    size);

                SpawnGameObject(spawnable, gameObjectInstance, spawnPosRot, color, optionals);
                k++;
            } while (k < n);
        }

        // count of parameter entries in a list
        // used for optional YAML parameters where list could be null
        private int optionalCount<T>(List<T> paramList) { return (paramList!=null) ? paramList.Count: 0; }

        private void SpawnAgent(Spawnable agentSpawnableFromUser)
        {
            PositionRotation agentToSpawnPosRot;
            Vector3 agentSize = _agent.transform.localScale;
            Vector3 position;
            float rotation;

            position = (agentSpawnableFromUser == null || !agentSpawnableFromUser.positions.Any()) ?
                             -Vector3.one : agentSpawnableFromUser.positions[0];
            rotation = (agentSpawnableFromUser == null || !agentSpawnableFromUser.rotations.Any()) ?
                             -1 : agentSpawnableFromUser.rotations[0];

            agentToSpawnPosRot = SamplePositionRotation(_agent,
                                                        _maxSpawnAttemptsForAgent,
                                                        position,
                                                        rotation,
                                                        agentSize);

            _agentRigidbody.angularVelocity = Vector3.zero;
            _agentRigidbody.velocity = Vector3.zero;
            _agent.transform.localPosition = agentToSpawnPosRot.Position;
            _agent.transform.rotation = Quaternion.Euler(agentToSpawnPosRot.Rotation);
        }

        private void SpawnGameObject(Spawnable spawnable,
                                     GameObject gameObjectInstance,
                                     PositionRotation spawnLocRot,
                                     Vector3 color,
                                     Dictionary<string, object> optionals = null)
        {
            // Debug.Log("Spawning: " + spawnable.name);
            if (spawnLocRot != null)
            {
                gameObjectInstance.transform.localPosition = spawnLocRot.Position;
                gameObjectInstance.transform.Rotate(spawnLocRot.Rotation);
                gameObjectInstance.SetLayer(0);
                gameObjectInstance.GetComponent<IPrefab>().SetColor(color);

                if (gameObjectInstance.CompareTag("goodGoalMulti") || gameObjectInstance.CompareTag("goodGoal"))
                {
                    _goodGoalsMultiSpawned.Add(gameObjectInstance.GetComponent<Goal>());
                }
                // check for optional symbol name for SignPosterboards
                if (optionals["symbolName"] != null) {
                    AssignSymbolName(gameObjectInstance, (string)optionals["symbolName"], color);
                }
                // now check all floats relating to timing of changes
                // each float param has a list of "acceptable types" to which it applies
                Dictionary<string, List<Type>> paramValidTypeLookup = new Dictionary<string, List<Type>> {
                    { "delay",          new List<Type> { typeof(DecayGoal), typeof(SizeChangeGoal), typeof(GoalSpawner)} },
                    { "initialValue",   new List<Type> { typeof(DecayGoal), typeof(SizeChangeGoal) } },
                    { "finalValue",     new List<Type> { typeof(DecayGoal), typeof(SizeChangeGoal) } },
                    { "changeRate",     new List<Type> { typeof(DecayGoal), typeof(SizeChangeGoal) } }
                };
                foreach (string paramKey in paramValidTypeLookup.Keys) {
                    // try each valid type that we might be able to assign to
                    if (optionals[paramKey] != null) {
                        foreach (Type T in paramValidTypeLookup[paramKey]) {
                            Component component = new Component();
                            // see if gameObjectInstance has got the relevant component
                            if (gameObjectInstance.TryGetComponent(T, out component))
                                AssignTimingFloat(paramKey, (float)optionals[paramKey], component);
                        }
                    }
                }
            }
            else
            {
                gameObjectInstance.SetActive(false);
                GameObject.Destroy(gameObjectInstance);
            }
        }

        // calls SetSymbol on SignPosterboard if such a component can be found - overrides colour setting also
        private void AssignSymbolName(GameObject gameObjectInstance, string sName, Vector3 color) {
            SignPosterboard SP = gameObjectInstance.GetComponent<SignPosterboard>();
            if (SP != null)
            {
                if (color != new Vector3(-1, -1, -1)) { SP.SetColourOverride(color, true); }
                // assertion-cast that symbolName is string (stored as object)
                SP.SetSymbol(sName, true); // UpdatePosterboard() for color/symbol texture is called here
            }
        }

        // calls correct Setter method according to arg paramName and corresponding method-name
        private void AssignTimingFloat<T>(string paramName, float value, T component) {
            paramName = paramName[0].ToString().ToUpper() + paramName.Substring(1); // "delay" -> "Delay" and so on...
            MethodInfo SetMethod = component.GetType().GetMethod("Set" + (paramName));
            Debug.Log("Trying to invoke method with name: " + "Set" + (paramName));
            Debug.Log("...for object of type " + component.GetType().ToString());
            if (SetMethod != null) { SetMethod.Invoke(component, new object[] { value }); }
        }

        private PositionRotation SamplePositionRotation(GameObject gameObjectInstance,
                                                        int maxSpawnAttempt,
                                                        Vector3 positionIn,
                                                        float rotationY,
                                                        Vector3 size)
        {
            Vector3 gameObjectBoundingBox;
            Vector3 rotationOut = Vector3.zero;
            Vector3 positionOut = Vector3.zero;
            IPrefab gameObjectInstanceIPrefab = gameObjectInstance.GetComponent<IPrefab>();
            bool canSpawn = false;
            int k = 0;

            while (!canSpawn && k < maxSpawnAttempt)
            {

                gameObjectInstanceIPrefab.SetSize(size);
                gameObjectBoundingBox = gameObjectInstance.GetBoundsWithChildren().extents;
                positionOut = gameObjectInstanceIPrefab.GetPosition(positionIn,
                                                                    gameObjectBoundingBox,
                                                                    _rangeX,
                                                                    _rangeZ);
                rotationOut = gameObjectInstanceIPrefab.GetRotation(rotationY);

                Collider[] colliders = Physics.OverlapBox(positionOut + _arena.position,
                                                            gameObjectBoundingBox,
                                                            Quaternion.Euler(rotationOut),
                                                            1 << 0);
                canSpawn = IsSpotFree(colliders, gameObjectInstance.CompareTag("agent"), gameObjectInstance.name.Contains("Zone")); //@To-DO maybe find a better way to do this zone name-check
                k++;

            }
            if (canSpawn)
            {
                return new PositionRotation(positionOut, rotationOut);
            }
            return null;
        }

        private bool IsSpotFree(Collider[] colliders, bool isAgent, bool isZone = false)
        {
            //if (isZone) Debug.Log("IsZone set to TRUE");
            if (isZone) return colliders.Length == 0 ||
                    (colliders.All(collider => collider.isTrigger || !collider.gameObject.CompareTag("arena")) && !isAgent);
            else return colliders.Length == 0 ||
                    (colliders.All(collider => collider.isTrigger) && !isAgent);
        }

        private bool ObjectOutsideOfBounds(Vector3 position, Vector3 boundingBox)
        {
            return position.x > boundingBox.x && position.x < _rangeX - boundingBox.x
                    && position.z > boundingBox.z && position.z < _rangeZ - boundingBox.z;
        }

        private void updateGoodGoalsMulti()
        {
            int numberOfGoals = _goodGoalsMultiSpawned.Count;
            foreach (Goal goodGoalMulti in _goodGoalsMultiSpawned)
            {
                goodGoalMulti.numberOfGoals = numberOfGoals;
            }
        }

    }
}

