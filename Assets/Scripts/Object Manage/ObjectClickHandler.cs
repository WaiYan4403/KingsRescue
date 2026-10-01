using UnityEngine;
using UnityEngine.EventSystems;

public class ObjectClickHandler : MonoBehaviour
{
    private ObjectHandler currentHoveredObject;

    ObjectHandler GetTopMostObjectHandler()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit2D[] hits = Physics2D.GetRayIntersectionAll(ray, 100f, LayerMask.GetMask("Interactable"));

        ObjectHandler bestHandler = null;
        int bestSortingLayerValue = int.MinValue;
        int bestSortingOrder = 0;
        float bestDistance = float.MaxValue;
        const float distanceEpsilon = 0.001f;

        foreach (var hit in hits)
        {
            var handler = hit.collider.GetComponent<ObjectHandler>();
            if (handler == null) continue;

            var renderer = hit.collider.GetComponent<Renderer>();
            int layerValue = 0;
            int order = 0;

            if (renderer != null)
            {
                layerValue = SortingLayer.GetLayerValueFromID(renderer.sortingLayerID);
                order = renderer.sortingOrder;
            }

            bool isCloser = hit.distance + distanceEpsilon < bestDistance;
            bool isSameDistance = Mathf.Abs(hit.distance - bestDistance) <= distanceEpsilon;

            bool isBetter =
                isCloser ||
                (isSameDistance && (layerValue > bestSortingLayerValue ||
                (layerValue == bestSortingLayerValue && order > bestSortingOrder)));

            if (isBetter)
            {
                bestSortingLayerValue = layerValue;
                bestSortingOrder = order;
                bestDistance = hit.distance;
                bestHandler = handler;
                //Debug.Log("Best handler: " + bestHandler.name);
                //Debug.Log("Best layer value: " + bestSortingLayerValue);
                //Debug.Log("Best sorting order: " + bestSortingOrder);
                //Debug.Log("Best distance: " + bestDistance);
            }
        }

        return bestHandler;
    }

    void Start()
    {

    }

    void Update()
    {
        HandleHover();

        if (Input.GetMouseButtonDown(0))
        {
            var objectHandler = GetTopMostObjectHandler();
            if (objectHandler == null)
                return;

            if (!objectHandler.isUIElement && EventSystem.current.IsPointerOverGameObject())
                return;

            objectHandler.OnLeftClicked.Invoke();
        }
    }

    void HandleHover()
    {
        var objectHandler = GetTopMostObjectHandler();

        if (objectHandler != null)
        {
            if (!objectHandler.isUIElement && EventSystem.current.IsPointerOverGameObject())
            {
                if (currentHoveredObject != null)
                {
                    //Debug.Log("Unhovered object: " + currentHoveredObject.name);
                    currentHoveredObject.OnUnhovered.Invoke();
                    currentHoveredObject = null;
                }
                return;
            }

            if (objectHandler != currentHoveredObject)
            {
                if (currentHoveredObject != null)
                {
                    //Debug.Log("Unhovered object: " + currentHoveredObject.name);
                    currentHoveredObject.OnUnhovered.Invoke();
                }

                //Debug.Log("Hovered object: " + objectHandler.name);
                currentHoveredObject = objectHandler;
                objectHandler.OnHovered.Invoke();
            }
        }
        else
        {
            if (currentHoveredObject != null)
            {
                //Debug.Log("Unhovered object: " + currentHoveredObject.name);
                currentHoveredObject.OnUnhovered.Invoke();
                currentHoveredObject = null;
            }
        }
    }
}
