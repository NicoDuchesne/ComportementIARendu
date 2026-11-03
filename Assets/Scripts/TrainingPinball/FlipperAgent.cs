using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using Unity.VisualScripting;
using UnityEngine;
using static Flipper;

public class FlipperAgent : Agent {

    static int NextAgentId = 0;

    [SerializeField] TrainingPinballArea trainingPinball;
    [SerializeField] Flipper.FlipperSide side = Flipper.FlipperSide.Left;

    int id = 0;
    int episodeId = 0;
    public int Id { get => id; private set => id = value; }

    PlayerController playerController;


    float buttonPressedTimestamp = 0f;
    float ballInContactTimestamp = 0f;
    float ballEnterFlipperTimestamp = 0f;
    float ballEnterOtherFlipperTimestamp = 0f;
    float episodeBeginTimestamp = 0f;

    bool BallInContact => Time.fixedTime <= ballInContactTimestamp + Time.fixedDeltaTime * 3; // Solves jittering at low speed. We allow 3 fixed update of before counting a new collision as an "OnCollisionEnter"    
    
    //MY VALUES
    private float minDistBallTarget = float.MaxValue;
    private float originalDistBallTarget = 0f;
    public float currentDistReward = 0f;
    public float MinDistBallTarget { get => minDistBallTarget; private set => minDistBallTarget = value; }
    public float OriginalDistBallTarget { get => originalDistBallTarget; private set => originalDistBallTarget = value; }
    private bool wentHigher = false;
    private float counter = 0f;
    private float maxStillTime = 3f;
    private bool wentAFK = false;

    void Awake() {
        playerController = new PlayerController();
        playerController.Enable();
        id = NextAgentId++;
    }

    void Start() {
        trainingPinball.Flipper(side).onBallEnter += HandleOnBallEnterFlipper;
        trainingPinball.Flipper(side).onBallStay += HandleOnBallInContactWithFlipper;
        trainingPinball.Flipper(side).onBallExit += HandleOnBallExitFlipper;
        if (trainingPinball.LeftFlipper) {
            trainingPinball.LeftFlipper.onBallEnter += HandleOnBallEnterAnyFlipper;
        }
        if (trainingPinball.RightFlipper) {
            trainingPinball.RightFlipper.onBallEnter += HandleOnBallEnterAnyFlipper;
        }

        trainingPinball.onBallEnterBound += HandleOnBallEnterBound;
        trainingPinball.onBallHitTarget += HandleOnBallHitTarget;
        trainingPinball.onEpisodeEnd += HandleOnEpisodeEnd;
    }

    private void OnDestroy() {
        trainingPinball.Flipper(side).onBallEnter -= HandleOnBallEnterFlipper;
        trainingPinball.Flipper(side).onBallStay -= HandleOnBallInContactWithFlipper;
        trainingPinball.Flipper(side).onBallExit -= HandleOnBallExitFlipper;
        if (trainingPinball.LeftFlipper) {
            trainingPinball.LeftFlipper.onBallEnter -= HandleOnBallEnterAnyFlipper;
        }
        if (trainingPinball.RightFlipper) {
            trainingPinball.RightFlipper.onBallEnter -= HandleOnBallEnterAnyFlipper;
        }

        trainingPinball.onBallEnterBound -= HandleOnBallEnterBound;
        trainingPinball.onBallHitTarget -= HandleOnBallHitTarget;
        trainingPinball.onEpisodeEnd -= HandleOnEpisodeEnd;
    }

    //Mes rewards du fixedUpdate
    private void FixedUpdate() {
        
        //Reward selon si on se rappoche de la target
        float distBallTarget = CalculateDistBallTarget();
        
        if (distBallTarget < minDistBallTarget)
        {
            minDistBallTarget = distBallTarget;
            RewardManager.Instance.AddReward(this, RewardType.DistBallTargetDown);
        }
        else
        {
            RewardManager.Instance.AddReward(this, RewardType.DistBallTargetUp);
        }
        
        //Reward négative si on dépasse la hauteur de la target
        Vector2 ballPos = new Vector2(trainingPinball.Ball.transform.position.x, trainingPinball.Ball.transform.position.y);
        Vector2 targetPos = new Vector2(trainingPinball.Target.transform.position.x, trainingPinball.Target.transform.position.y);
        if (ballPos.y > targetPos.y && !wentHigher)
        {
            wentHigher = true;
            RewardManager.Instance.AddReward(this, RewardType.BallWentHigher);
        }

        //Reward négative si on empêche le déroulement de la partie en stoppant la balle
        if (trainingPinball.Ball.linearVelocity.magnitude < 2f)
        {
            counter += Time.fixedDeltaTime;
            if (counter > maxStillTime && !wentAFK)
            {
                RewardManager.Instance.AddReward(this, RewardType.BallStayStill);
                wentAFK = true;
            }
        }
        
        //Reward négative pour empêcher un flipper de retenir la balle tout en la gardant en mouvement
        float max = trainingPinball.Configuration.FlipperAngleOffsetMax;
        float actual = trainingPinball.Flipper(side).CurrentAngleOffset;
        if (Mathf.Lerp(0f, 1f, actual / max) > 0.65f && BallInContact)
        {
            RewardManager.Instance.AddReward(this, RewardType.FlipperStaysUp);
        }
    }

    public override void OnEpisodeBegin() {
        trainingPinball.BeginEpisode();
        buttonPressedTimestamp = 0f;
        ballInContactTimestamp = 0f;
        ballEnterFlipperTimestamp = 0f;
        ballEnterOtherFlipperTimestamp = 0;
        episodeBeginTimestamp = Time.fixedTime;
        
        //reset de mes valeurs
        originalDistBallTarget = CalculateDistBallTarget();
        minDistBallTarget = originalDistBallTarget;
        currentDistReward = 0f;
        wentHigher = false;
        counter = 0f;
        wentAFK = false;
    }

    public override void CollectObservations(VectorSensor sensor) {
        //OBSERVATIONS
        
        //Vecteur Flipper-Ball
        Vector3 v = trainingPinball.Ball.transform.position - trainingPinball.Flipper(side).transform.position;
        Vector2 dim = trainingPinball.Configuration.AveragePinballDimension;
        Vector2 vectorFlipperBall = new Vector2(v.x/dim.x, v.y/dim.y);
        vectorFlipperBall = trainingPinball.Flipper(side).WorldToLocalVector(vectorFlipperBall);
        
        //Position de la Ball
        Vector2 ballPos = new Vector2(trainingPinball.Ball.transform.position.x, trainingPinball.Ball.transform.position.y);
        
        //Position de la Target
        Vector2 targetPos = new Vector2(trainingPinball.Target.transform.position.x, trainingPinball.Target.transform.position.y);
        
        //Distance Ball-Target
        float distBallTarget = Vector2.Distance(ballPos, targetPos);
        
        //Vecteur Ball-Target
        v = trainingPinball.Target.transform.position - trainingPinball.Ball.transform.position;
        Vector2 vectorBallTarget = new Vector2(v.x/dim.x, v.y/dim.y);
        
        //Ball Velocity
        Vector2 ballVelocity = trainingPinball.Ball.linearVelocity;
        
        //Flipper angle offset
        float max = trainingPinball.Configuration.FlipperAngleOffsetMax;
        float actual = trainingPinball.Flipper(side).CurrentAngleOffset;
        float flipperAngle = Mathf.Lerp(0f, 1f, actual/max);
        
        //Left or right side
        float sideValue = side == Flipper.FlipperSide.Left ? -1f : 1f;
        
        //Ball In Contact
        
        //ADD OBSERVATIONS
        sensor.AddObservation(ballPos);
        sensor.AddObservation(targetPos);
        sensor.AddObservation(distBallTarget);
        sensor.AddObservation(vectorBallTarget);
        sensor.AddObservation(vectorFlipperBall);
        sensor.AddObservation(ballVelocity);
        sensor.AddObservation(flipperAngle);
        sensor.AddObservation(BallInContact);
        sensor.AddObservation(sideValue);
    }

    public override void OnActionReceived(ActionBuffers actions) {
        bool isPressed = actions.DiscreteActions[0] > 0;

        if (!trainingPinball.IsButtonPressed(side) && isPressed) {
            buttonPressedTimestamp = Time.fixedTime;
            
            //Reward si on actionne avec la balle ou non
            if (BallInContact)
            {
                RewardManager.Instance.AddReward(this, RewardType.ShootWithBall);
            }
            else
            {
                RewardManager.Instance.AddReward(this, RewardType.ShootWithoutBall);
            }
        }

        trainingPinball.SetButtonState(isPressed, side);
    }

    public override void Heuristic(in ActionBuffers actionsOut) {
        var discreteActionsOut = actionsOut.DiscreteActions;
        bool isPressed = side == Flipper.FlipperSide.Left ? playerController.Player.LeftFlipper.IsPressed() : playerController.Player.RightFlipper.IsPressed();
        discreteActionsOut[0] = isPressed ? 1 : 0;
    }

    void HandleOnEpisodeEnd() {
        RewardManager.Instance.UpdateAvgRewardsEpisode(this);//update pour le diagnostic
        EndEpisode();
        episodeId++;
    }



    void HandleOnBallEnterFlipper(FlipperSide side) {
        // Solves jittering at low speed. We allow 3 fixed update of before counting a new collision as an "OnCollisionEnter"
        if (!BallInContact) {
            ballEnterFlipperTimestamp = Time.fixedTime;
            //Debug.Log("HandleOnBallEnterFlipper");
        }

        HandleOnBallInContactWithFlipper(side);
    }

    void HandleOnBallInContactWithFlipper(FlipperSide side) {
        ballInContactTimestamp = Time.fixedTime;

    }

    void HandleOnBallExitFlipper(FlipperSide side) {
        // Do not use. May be called multiple times due to jittering
    }

    void HandleOnBallHitTarget() {
        //Reward quand on hit la target
        RewardManager.Instance.AddReward(this, RewardType.BallHitTarget);
    }

    void HandleOnBallEnterAnyFlipper(FlipperSide side) {
        if (side != this.side) {
            ballEnterOtherFlipperTimestamp = Time.fixedTime;
        }

    }

    void HandleOnBallEnterBound(BoundTrigger.BoundType type) {
        //Différentes rewards lorsqu'on touche des bounds
        switch (type)
        {
            case BoundTrigger.BoundType.Drain :
                RewardManager.Instance.AddReward(this, RewardType.BallHitDrain);
                break;
            case BoundTrigger.BoundType.ResetZone :
                RewardManager.Instance.AddReward(this, RewardType.BallHitResetZone);
                break;
            case BoundTrigger.BoundType.OtherSide :
                break;
        }
    }


    //fonction pratique pour la distance ball target
    private float CalculateDistBallTarget()
    {
        Vector2 ballPos = new Vector2(trainingPinball.Ball.transform.position.x, trainingPinball.Ball.transform.position.y);
        Vector2 targetPos = new Vector2(trainingPinball.Target.transform.position.x, trainingPinball.Target.transform.position.y);
        float result = Vector2.Distance(ballPos, targetPos);

        return result;
    }
    
    //tentative de fonction pour normaliser les observations vecteurs
    // private Vector2 NormalizePos(Vector3 worldPos)
    // {
    //     Vector2 dim = trainingPinball.Configuration.AveragePinballDimension;
    //     Vector3 local = trainingPinball.transform.InverseTransformPoint(worldPos);
    //     return new Vector2(local.x / dim.x, local.y / dim.y);
    // }
    
    
    

}
