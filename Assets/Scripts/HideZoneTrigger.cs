using UnityEngine;

public class HideZoneTrigger : MonoBehaviour
{
    [SerializeField] private GameObject drawingPanel;

    private void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log(
            $"[HideZoneTrigger] Trigger entered - Object: '{other.name}', Tag: '{other.tag}'",
            this);

        // Player가 아닌 오브젝트와의 접촉은 무시합니다.
        if (!other.CompareTag("Player"))
        {
            return;
        }

        if (drawingPanel == null)
        {
            Debug.LogError("[HideZoneTrigger] Drawing Panel is not assigned.", this);
            return;
        }

        // HideZone 안에 들어오면 기존 그림이 담긴 DrawingPanel을 표시합니다.
        drawingPanel.SetActive(true);
        Debug.Log(
            $"[HideZoneTrigger] Drawing Panel '{drawingPanel.name}' activated.",
            drawingPanel);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        Debug.Log(
            $"[HideZoneTrigger] Trigger exited - Object: '{other.name}', Tag: '{other.tag}'",
            this);

        // Player가 아닌 오브젝트가 나간 경우에는 아무 작업도 하지 않습니다.
        if (!other.CompareTag("Player"))
        {
            return;
        }

        if (drawingPanel == null)
        {
            Debug.LogError("[HideZoneTrigger] Drawing Panel is not assigned.", this);
            return;
        }

        // 패널만 숨기고 DrawingTest.ClearCanvas()는 호출하지 않아 그림을 유지합니다.
        drawingPanel.SetActive(false);
        Debug.Log(
            $"[HideZoneTrigger] Drawing Panel '{drawingPanel.name}' hidden.",
            drawingPanel);
    }
}
