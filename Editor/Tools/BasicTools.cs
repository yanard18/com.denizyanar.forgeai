using UnityEngine;
using UnityEditor;

namespace ForgeAI
{
    public static class BasicTools
    {
        [ForgeTool("Creates a primitive object (Cube, Sphere, Capsule, Cylinder, Plane, Quad)", "primitiveType:string, name:string")]
        public static string CreatePrimitive(string primitiveType, string name)
        {
            if (System.Enum.TryParse(primitiveType, true, out PrimitiveType type))
            {
                GameObject obj = GameObject.CreatePrimitive(type);
                if (!string.IsNullOrEmpty(name)) obj.name = name;
                Selection.activeGameObject = obj;
                return $"Success: Created {primitiveType} named '{obj.name}' at {obj.transform.position}.";
            }
            return $"Error: Unknown primitive type '{primitiveType}'.";
        }

        [ForgeTool("Logs a message to the Unity Console", "message:string")]
        public static string LogMessage(string message)
        {
            Debug.Log($"[ForgeAI] {message}");
            return "Success: Logged message.";
        }
        
        [ForgeTool("Finds a GameObject by name", "name:string")]
        public static string FindObject(string name)
        {
            var obj = GameObject.Find(name);
            if (obj != null)
            {
                return $"Success: Found object '{obj.name}' with InstanceID {obj.GetInstanceID()}.";
            }
            return $"Error: Object '{name}' not found.";
        }
    }
}
