using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

public class TerrainWalkerAgent : MarathonAgent {

    TerrainGenerator _terrainGenerator;

    int _lastXPosInMeters;
    int _stepCountAtLastMeter;
    float _pain;
    Vector3 _centerOfMass;

    public override void OnEpisodeBegin()
    {
        base.OnEpisodeBegin();

        BodyParts["pelvis"] = GetComponentsInChildren<Rigidbody>().FirstOrDefault(x => x.name == "torso");
        BodyParts["left_thigh"] = GetComponentsInChildren<Rigidbody>().FirstOrDefault(x => x.name == "left_thigh");
        BodyParts["right_thigh"] = GetComponentsInChildren<Rigidbody>().FirstOrDefault(x => x.name == "right_thigh");
        BodyParts["right_foot"] = GetComponentsInChildren<Rigidbody>().FirstOrDefault(x => x.name == "right_foot");
        BodyParts["left_foot"] = GetComponentsInChildren<Rigidbody>().FirstOrDefault(x => x.name == "left_foot");

        SetCenterOfMass();

        if (_terrainGenerator == null)
            _terrainGenerator = GetComponent<TerrainGenerator>();
        _lastXPosInMeters = (int) BodyParts["pelvis"].transform.position.x;
        _terrainGenerator.Reset();
        _stepCountAtLastMeter = 0;

        // set to true this to show monitor while training
        //Monitor.SetActive(true);

        StepRewardFunction = StepRewardWalker106;
        TerminateFunction = LocalTerminate;
        ObservationsFunction = ObservationsDefault;
        OnTerminateRewardValue = 0f;
        _pain = 0f;

        base.SetupBodyParts();
        SetCenterOfMass();
    }

    bool LocalTerminate()
    {
        int newXPosInMeters = (int) BodyParts["pelvis"].transform.position.x;
        if (newXPosInMeters > _lastXPosInMeters) {
            _lastXPosInMeters = newXPosInMeters;
            _stepCountAtLastMeter = this.StepCount;
        }

        SetCenterOfMass();
        var xpos = _centerOfMass.x;
        var terminate = false;
        if (this.StepCount-_stepCountAtLastMeter >= (100*5))
            terminate = true;
        else if (xpos < 4f && _pain > 1f)
            terminate = true;
        else if (xpos < 2f && _pain > 0f)
            terminate = true;

        return terminate;
    }
    public override void OnTerrainCollision(GameObject other, GameObject terrain) {
        if (terrain.GetComponent<Terrain>() == null)
            return;

        switch (other.name.ToLowerInvariant().Trim())
        {
            case "pelvis": // dm_hopper
                _pain += 5f;
                NonFootHitTerrain = true;
                break;
            case "right_leg": // dm_walker
            case "left_leg": // dm_walker
            case "right_foot": // dm_walker
            case "left_foot": // dm_walker
                FootHitTerrain = true;
                break;
            default:
                _pain += 5f;
                NonFootHitTerrain = true;
                break;
        }
    }
  
    void ObservationsDefault(VectorSensor sensor)
    {
        var pelvis = BodyParts["pelvis"];
        Vector3 normalizedVelocity = this.GetNormalizedVelocity(pelvis.velocity);
        sensor.AddObservation(normalizedVelocity);
        sensor.AddObservation(pelvis.transform.forward); // gyroscope 
        sensor.AddObservation(pelvis.transform.up);

        sensor.AddObservation(SensorIsInTouch);
        JointRotations.ForEach(x => sensor.AddObservation(x));
        sensor.AddObservation(JointVelocity);
        // sensor.AddObservation(new []{
        //     this.GetNormalizedPosition(BodyParts["left_foot"].transform.position).y,
        //     this.GetNormalizedPosition(BodyParts["right_foot"].transform.position).y
        // });

        (List<float> distances, float fraction) = 
            _terrainGenerator.GetDistances2d(
                pelvis.transform.position, ShowMonitor);
   
        sensor.AddObservation(distances);
        sensor.AddObservation(fraction);
    }


    void SetCenterOfMass()
    {
        _centerOfMass = Vector3.zero;
        float c = 0f;
        var bodyParts = this.gameObject.GetComponentsInChildren<Rigidbody>();
 
        foreach (var part in bodyParts)
        {
            _centerOfMass += part.worldCenterOfMass * part.mass;
            c += part.mass;
        }
        _centerOfMass /= c;
    }

    float StepRewardWalker106()
    {
        float uprightBonus = GetDirectionBonus("pelvis", Vector3.forward, 1f);
        uprightBonus = Mathf.Clamp(uprightBonus, 0f, 1f);
        float velocity = Mathf.Clamp(GetNormalizedVelocity("pelvis").x, 0f, 1f);
        float effort = 1f - GetEffortNormalized();

        //if (ShowMonitor)
        //{
        //    var hist = new[] {velocity, uprightBonus, effort}.ToList();
        //    Monitor.Log("rewardHist", hist.ToArray(), displayType: Monitor.DisplayType.Independent);
        //}

        // uprightBonus *= 0.1f;
        // velocity *= 0.45f;
        // if (velocity >= .45f)
        //     effort *= 0.45f;
        // else
        //     effort *= velocity;

        // var reward = velocity
        //              + uprightBonus
        //              + effort;
        var reward = velocity;
        _pain = 0f;
        return reward;
    }
}
