using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;

public class EasyIK : MonoBehaviour
{
    [Header("IK properties")]
    public int numberOfJoints = 2;
    public Transform ikTarget;
    public int iterations = 10;
    public float tolerance = 0.05f;
    private Transform[] jointTransforms;
    private Vector3 startPosition;
    private Vector3[] jointPositions;
    private float[] boneLength;
    private float jointChainLength;
    private float distanceToTarget;
    private Quaternion[] startRotation;
    private Vector3[] jointStartDirection;
    private Quaternion ikTargetStartRot;
    private Quaternion lastJointStartRot;

    [Header("Pole target (3 joint chain)")]
    public Transform poleTarget;

    [Header("Debug")]
    public bool debugJoints = true;
    public bool localRotationAxis = false;

    // Remove this if you need bigger gizmos:
    [Range(0.0f, 1.0f)]
    public float gizmoSize = 0.05f;
    public bool poleDirection = false;
    public bool poleRotationAxis = false;

    void Awake()
    {
        // Let's set some properties
        jointChainLength = 0;
        jointTransforms = new Transform[numberOfJoints];
        jointPositions = new Vector3[numberOfJoints];
        boneLength = new float[numberOfJoints];
        jointStartDirection = new Vector3[numberOfJoints];
        startRotation = new Quaternion[numberOfJoints];
        ikTargetStartRot = ikTarget.rotation;

        var current = transform;

        // For each bone calculate and store the lenght of the bone
        for (var i = 0; i < jointTransforms.Length; i += 1)
        {
            jointTransforms[i] = current;
            // If the bones lenght equals the max lenght, we are on the last joint in the chain
            if (i == jointTransforms.Length - 1)
            {
                lastJointStartRot = current.rotation;
                return;
            }
            // Store length and add the sum of the bone lengths
            else
            {
                boneLength[i] = Vector3.Distance(current.position, current.GetChild(0).position);
                jointChainLength += boneLength[i];

                jointStartDirection[i] = current.GetChild(0).position - current.position;
                startRotation[i] = current.rotation;
            }
            // Move the iteration to next joint in the chain
            current = current.GetChild(0);
        }
    }

    void Start()
    {

    }

    void PoleConstraint()
    {
        if (poleTarget != null && numberOfJoints < 4)
        {
            // Get the limb axis direction
            var limbAxis = (jointPositions[2] - jointPositions[0]).normalized;

            // Get the direction from the root joint to the pole target and mid joint
            Vector3 poleDirection = (poleTarget.position - jointPositions[0]).normalized;
            Vector3 boneDirection = (jointPositions[1] - jointPositions[0]).normalized;
            
            // Ortho-normalize the vectors
            Vector3.OrthoNormalize(ref limbAxis, ref poleDirection);
            Vector3.OrthoNormalize(ref limbAxis, ref boneDirection);

            // Calculate the angle between the boneDirection vector and poleDirection vector
            Quaternion angle = Quaternion.FromToRotation(boneDirection, poleDirection);

            // Rotate the middle bone using the angle
            jointPositions[1] = angle * (jointPositions[1] - jointPositions[0]) + jointPositions[0];
        }
    }

    void Backward()
    {
        // Iterate through every position in the list until we reach the start of the chain
        for (int i = jointPositions.Length - 1; i >= 0; i -= 1)
        {
            // The last bone position should have the same position as the ikTarget
            if (i == jointPositions.Length - 1)
            {
                jointPositions[i] = ikTarget.transform.position;
            }
            else
            {
                jointPositions[i] = jointPositions[i + 1] + (jointPositions[i] - jointPositions[i + 1]).normalized * boneLength[i];
            }
        }
    }

    void Forward()
    {
        // Iterate through every position in the list until we reach the end of the chain
        for (int i = 0; i < jointPositions.Length; i += 1)
        {
            // The first bone position should have the same position as the startPosition
            if (i == 0)
            {
                jointPositions[i] = startPosition;
            }
            else
            {
                jointPositions[i] = jointPositions[i - 1] + (jointPositions[i] - jointPositions[i - 1]).normalized * boneLength[i - 1];
            }
        }
    }

    private void SolveIK()
    {
        // Get the jointPositions from the joints
        for (int i = 0; i < jointTransforms.Length; i += 1)
        {
            jointPositions[i] = jointTransforms[i].position;
        }
        // Distance from the root to the ikTarget
        distanceToTarget = Vector3.Distance(jointPositions[0], ikTarget.position);

        // IF THE TARGET IS NOT REACHABLE
        if (distanceToTarget > jointChainLength)
        {
            // Direction from root to ikTarget
            var direction = ikTarget.position - jointPositions[0];

            // Get the jointPositions
            for (int i = 1; i < jointPositions.Length; i += 1)
            {
                jointPositions[i] = jointPositions[i - 1] + direction.normalized * boneLength[i - 1];
            }
        }
        // IF THE TARGET IS REACHABLE
        else
        {
            // Get the distance from the leaf bone to the ikTarget
            float distToTarget = Vector3.Distance(jointPositions[jointPositions.Length - 1], ikTarget.position);
            float counter = 0;
            // While the distance to target is greater than the tolerance let's iterate until we are close enough
            while (distToTarget > tolerance)
            {
                startPosition = jointPositions[0];
                Backward();
                Forward();
                counter += 1;
                // After x iterations break the loop to avoid an infinite loop
                if (counter > iterations)
                {
                    break;
                }
            }
        }
        // Apply the pole constraint
        PoleConstraint();

        // Apply the jointPositions and rotations to the joints
        for (int i = 0; i < jointPositions.Length - 1; i += 1)
        {
            jointTransforms[i].position = jointPositions[i];
            var targetRotation = Quaternion.FromToRotation(jointStartDirection[i], jointPositions[i + 1] - jointPositions[i]);
            jointTransforms[i].rotation = targetRotation * startRotation[i];
        }
        // Let's constrain the rotation of the last joint to the IK target and maintain the offset.
        Quaternion offset = lastJointStartRot * Quaternion.Inverse(ikTargetStartRot);
        jointTransforms.Last().rotation = ikTarget.rotation * offset;
    }

    void Update()
    {
        SolveIK();
    }

    // Visual debugging
    void OnDrawGizmos()
    {   
        if (debugJoints == true)
        {   
            var current = transform;
            var child = transform.GetChild(0);

            for (int i = 0; i < numberOfJoints; i += 1)
            {
                if (i == numberOfJoints - 2)
                {
                    var length = Vector3.Distance(current.position, child.position);
                    DrawWireCapsule(current.position + (child.position - current.position).normalized * length / 2, Quaternion.FromToRotation(Vector3.up, (child.position - current.position).normalized), gizmoSize, length, Color.cyan);
                    break;
                }
                else
                {
                    var length = Vector3.Distance(current.position, child.position);
                    DrawWireCapsule(current.position + (child.position - current.position).normalized * length / 2, Quaternion.FromToRotation(Vector3.up, (child.position - current.position).normalized), gizmoSize, length, Color.cyan);
                    current = current.GetChild(0);
                    child = current.GetChild(0);
                }
            }
        }

        if (localRotationAxis == true)
        {    
            var current = transform;
            for (int i = 0; i < numberOfJoints; i += 1)
            {
                if (i == numberOfJoints - 1)
                {
                    drawHandle(current);
                }
                else
                {
                    drawHandle(current);
                    current = current.GetChild(0);
                }
            }
        }

        var start = transform;
        var mid = start.GetChild(0);
        var end = mid.GetChild(0);

        if (poleRotationAxis == true && poleTarget != null && numberOfJoints < 4)
        {    
            Handles.color = Color.white;
            Handles.DrawLine(start.position, end.position);
        }

        if (poleDirection == true && poleTarget != null && numberOfJoints < 4)
        {    
            Handles.color = Color.grey;
            Handles.DrawLine(start.position, poleTarget.position);
            Handles.DrawLine(end.position, poleTarget.position);
        }

    }

    void drawHandle(Transform debugJoint)
    {
        Handles.color = Handles.xAxisColor;
        Handles.ArrowHandleCap(0, debugJoint.position, debugJoint.rotation * Quaternion.LookRotation(Vector3.right), gizmoSize, EventType.Repaint);
                
        Handles.color = Handles.yAxisColor;
        Handles.ArrowHandleCap(0, debugJoint.position, debugJoint.rotation * Quaternion.LookRotation(Vector3.up), gizmoSize, EventType.Repaint);

        Handles.color = Handles.zAxisColor;
        Handles.ArrowHandleCap(0, debugJoint.position, debugJoint.rotation * Quaternion.LookRotation(Vector3.forward), gizmoSize, EventType.Repaint);
    }

     public static void DrawWireCapsule(Vector3 _pos, Quaternion _rot, float _radius, float _height, Color _color = default(Color))
     {
        Handles.color = _color;
        Matrix4x4 angleMatrix = Matrix4x4.TRS(_pos, _rot, Handles.matrix.lossyScale);
        using (new Handles.DrawingScope(angleMatrix))
        {
            var pointOffset = (_height - (_radius * 2)) / 2;

            Handles.DrawWireArc(Vector3.up * pointOffset, Vector3.left, Vector3.back, -180, _radius);
            Handles.DrawLine(new Vector3(0, pointOffset, -_radius), new Vector3(0, -pointOffset, -_radius));
            Handles.DrawLine(new Vector3(0, pointOffset, _radius), new Vector3(0, -pointOffset, _radius));
            Handles.DrawWireArc(Vector3.down * pointOffset, Vector3.left, Vector3.back, 180, _radius);

            Handles.DrawWireArc(Vector3.up * pointOffset, Vector3.back, Vector3.left, 180, _radius);
            Handles.DrawLine(new Vector3(-_radius, pointOffset, 0), new Vector3(-_radius, -pointOffset, 0));
            Handles.DrawLine(new Vector3(_radius, pointOffset, 0), new Vector3(_radius, -pointOffset, 0));
            Handles.DrawWireArc(Vector3.down * pointOffset, Vector3.back, Vector3.left, -180, _radius);

            Handles.DrawWireDisc(Vector3.up * pointOffset, Vector3.up, _radius);
            Handles.DrawWireDisc(Vector3.down * pointOffset, Vector3.up, _radius);
         }
     }
}
