using UnityEngine;

public class InputManager : MonoBehaviour
{
    void Update()
    {
        // Mouse (Editor + PC)
        if (Input.GetMouseButtonDown(0))
        {
            HandleScreenPoint(Input.mousePosition);
        }
        // Touch (Mobile)
        else if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            HandleScreenPoint(Input.GetTouch(0).position);
        }
    }

    void HandleScreenPoint(Vector2 screenPos)
    {
        Ray ray = Camera.main.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            // Cell directement
            Cell cell = hit.collider.GetComponent<Cell>();
            if (cell != null) { cell.HandleClick(); return; }

            // Player sur une cell
            Player player = hit.collider.GetComponent<Player>();
            if (player != null)
            {
                Vector2Int pos = player.gridManager.GetCellFromWorldPosition(player.transform.position);
                Cell targetCell = player.gridManager.gridCells[pos.x, pos.y]?.GetComponent<Cell>();
                targetCell?.HandleClick();
            }
        }
    }
}