using UnityEngine;

/// <summary>
/// Test script to verify DayNightCycle functionality
/// DayNightCycle işlevselliğini doğrulamak için test betiği
/// </summary>
public class DayNightCycleTest : MonoBehaviour
{
    [SerializeField] private DayNightCycle dayNightCycle;
    [SerializeField] private bool runTests = false;
    
    void Start()
    {
        if (runTests && dayNightCycle != null)
        {
            RunTests();
        }
    }
    
    private void RunTests()
    {
        Debug.Log("=== Starting DayNightCycle Tests ===");
        
        // Test 1: Time progression
        Debug.Log("Test 1: Time Progression");
        dayNightCycle.SetCurrentTime(0f);
        Debug.Log($"Set time to 0:00 - Current time: {dayNightCycle.GetCurrentTime():F2}");
        
        dayNightCycle.SetCurrentTime(6f);
        Debug.Log($"Set time to 6:00 (sunrise) - Is Daytime: {dayNightCycle.IsDaytime()}");
        
        dayNightCycle.SetCurrentTime(12f);
        Debug.Log($"Set time to 12:00 (noon) - Is Daytime: {dayNightCycle.IsDaytime()}");
        
        dayNightCycle.SetCurrentTime(18f);
        Debug.Log($"Set time to 18:00 (sunset) - Is Nighttime: {dayNightCycle.IsNighttime()}");
        
        dayNightCycle.SetCurrentTime(24f);
        Debug.Log($"Set time to 24:00 (midnight) - Is Nighttime: {dayNightCycle.IsNighttime()}");
        
        // Test 2: Day/Night detection
        Debug.Log("\nTest 2: Day/Night Detection");
        for (float time = 0f; time < 24f; time += 3f)
        {
            dayNightCycle.SetCurrentTime(time);
            string period = dayNightCycle.IsDaytime() ? "Day" : "Night";
            Debug.Log($"Time {time:F0}:00 - Period: {period}");
        }
        
        // Test 3: Boundary conditions
        Debug.Log("\nTest 3: Boundary Conditions");
        dayNightCycle.SetCurrentTime(5.99f);
        Debug.Log($"Time 5:59 - Is Nighttime: {dayNightCycle.IsNighttime()} (should be true)");
        
        dayNightCycle.SetCurrentTime(6.01f);
        Debug.Log($"Time 6:01 - Is Daytime: {dayNightCycle.IsDaytime()} (should be true)");
        
        dayNightCycle.SetCurrentTime(17.99f);
        Debug.Log($"Time 17:59 - Is Daytime: {dayNightCycle.IsDaytime()} (should be true)");
        
        dayNightCycle.SetCurrentTime(18.01f);
        Debug.Log($"Time 18:01 - Is Nighttime: {dayNightCycle.IsNighttime()} (should be true)");
        
        Debug.Log("\n=== DayNightCycle Tests Completed ===");
        
        // Reset to starting time
        dayNightCycle.SetCurrentTime(6f);
    }
    
    void Update()
    {
        if (dayNightCycle != null && Input.GetKeyDown(KeyCode.T))
        {
            RunTests();
        }
        
        // Display current time
        if (dayNightCycle != null && Input.GetKeyDown(KeyCode.I))
        {
            float time = dayNightCycle.GetCurrentTime();
            int hours = Mathf.FloorToInt(time);
            int minutes = Mathf.FloorToInt((time - hours) * 60f);
            string period = dayNightCycle.IsDaytime() ? "Day" : "Night";
            Debug.Log($"Current Time: {hours:D2}:{minutes:D2} - {period}");
        }
    }
}
