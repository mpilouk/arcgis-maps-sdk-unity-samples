using Esri.ArcGISMapsSDK.Components;
using Esri.ArcGISMapsSDK.Utils.GeoCoord;
using Esri.GameEngine.Geometry;
using Esri.HPFramework;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class RouteTraverser : MonoBehaviour
{
    public GameObject Vehicle;
    public GameObject Marker;
    public float Scale = 500f;
    public float Speed = 300f;
    public float RotationSpeed = 2f;
    public float closestDistance = 1.0f;

    public GameObject routeManager;
    private RouteManager routeManagerComponent;
    private int currentTraverserPosition = 0;

    private HPRoot hpRoot;
    private ArcGISMapComponent arcGISMapComponent;

    private float elevationOffset = 0.0f;
    double3 lastRootPosition;

    private bool reverseDirection = false;

    // Start is called before the first frame update
    void Start()
    {
        routeManagerComponent = routeManager.GetComponent<RouteManager>();
        // We need HPRoot for the HitToGeoPosition Method
        hpRoot = FindObjectOfType<HPRoot>();

        // We need this ArcGISMapComponent for the FromCartesianPosition Method
        // defined on the ArcGISMapComponent.View
        arcGISMapComponent = FindObjectOfType<ArcGISMapComponent>();

        lastRootPosition = arcGISMapComponent.GetComponent<HPRoot>().RootUniversePosition;
    }

    // Update is called once per frame
    void Update()
    {
        if (routeManager == null || routeManagerComponent.RoutingStatus() == true)
        {
            return;
        }

        GameObject[] routePoints = routeManagerComponent.GetRoutePoints().ToArray();
        if (routePoints.Length == 0)
        {
            return;
        }

        if (reverseDirection == false && currentTraverserPosition >= routePoints.Length)
        {
            currentTraverserPosition = routePoints.Length - 1;
            reverseDirection = true;
        }

        if (reverseDirection == true && currentTraverserPosition <= 0)
        {
            currentTraverserPosition = 0;
            reverseDirection = false;
        }

        if (reverseDirection == true && currentTraverserPosition >= routePoints.Length)
        {
            currentTraverserPosition = 0;
            reverseDirection = false;
        }

        GameObject routePoint = routePoints[currentTraverserPosition];

        var rpLocationComponent = routePoint.GetComponent<ArcGISLocationComponent>();

        var routeTraverserName = "Vehicle";
        GameObject routeTraverser = GameObject.Find(routeTraverserName);
        if (routeTraverser == null)
        {
            routeTraverser = Instantiate(Vehicle, Vector3.zero, Quaternion.identity, this.transform);
            routeTraverser.name = routeTraverserName;
            routeTraverser.SetActive(true);
            var hpTransform = routeTraverser.GetComponent<HPTransform>();
            hpTransform.LocalScale = new Vector3(Scale, Scale, Scale);

            var rtLocationComponent = routeTraverser.GetComponent<ArcGISLocationComponent>();
            rtLocationComponent.enabled = true;
            rtLocationComponent.Position = rpLocationComponent.Position;
            rtLocationComponent.Rotation = new ArcGISRotation(0, 90, 0);

            //SetElevation(routeTraverser);
            //RebaseCar(routeTraverser);
        }
        else
        {
            var rtLocationComponent = routeTraverser.GetComponent<ArcGISLocationComponent>();
            if (Vector3.Distance(routePoint.transform.position, routeTraverser.transform.position) < closestDistance)
            {
                if (reverseDirection)
                {
                    currentTraverserPosition--;
                }
                else
                {
                    currentTraverserPosition++;
                }
            }
            else
            {
                // Get the next routPoint
                // Set its elevation
                // Get the car position
                // Set its elevation
                // Compute the look at direction
                SetElevation(routePoint);
                SetElevation(routeTraverser);

                Vector3 lookAtDestination = new Vector3(routePoint.transform.position.x,
                        routePoint.transform.position.y, routePoint.transform.position.z);
                Vector3 direction = lookAtDestination - routeTraverser.transform.position;
                routeTraverser.transform.rotation = Quaternion.Slerp(routeTraverser.transform.rotation,
                        Quaternion.LookRotation(direction), Time.deltaTime * RotationSpeed);
                //routeTraverser.transform.Translate(0.0f, 0.0f, Speed * Time.deltaTime);
                routeTraverser.transform.localScale = new Vector3(1, 1, 1) * Scale;

                routeTraverser.transform.position = Vector3.MoveTowards(routeTraverser.transform.position,
                    routePoint.transform.position, Speed * Time.deltaTime);

                RebaseCar(routeTraverser);               
            }
        }
    }
    
    // Does a raycast to find the ground
    void SetElevation(GameObject clampedToGround)
    {
        // start the raycast in the air at an arbitrary to ensure it is above the ground
        var raycastHeight = 5000;
        var position = clampedToGround.transform.position;
        Debug.Log("Position " + position.x + ", " + position.y + ", " + position.z);
        var locationComponent = clampedToGround.GetComponent<ArcGISLocationComponent>();
        Debug.Log("Location Position " + locationComponent.Position.X + ", " + locationComponent.Position.Y + ", " + locationComponent.Position.Z);
        var raycastStart = new Vector3(position.x, position.y + raycastHeight, position.z);
        int layerMask = 1 << 3;
        layerMask = ~layerMask;
        if (Physics.Raycast(raycastStart, Vector3.down, out RaycastHit hitInfo, 6000, layerMask))
        {
            if (hitInfo.transform.name == "Vehicle")
			{
                Debug.Log("Hit Vehicle, ignored");
                return;
			}
            //Debug.Log("Hit " + hitInfo.transform.name + ": " + hitInfo.point.x + ", " + hitInfo.point.y + ", " + hitInfo.point.z);
            var geoPosition = HitToGeoPosition(hitInfo);
            Debug.Log("HitGeoPosition " + hitInfo.transform.name + ": " + geoPosition.X + ", " + geoPosition.Y + ", " + geoPosition.Z);
            //DrawDebugHit(hitInfo);
            locationComponent.Position = new ArcGISPoint(locationComponent.Position.X, locationComponent.Position.Y, geoPosition.Z);
        }
    }

    private void DrawDebugHit(RaycastHit hitInfo)
    {
        GameObject hitGobj = Instantiate(Marker, arcGISMapComponent.transform);

        var geoPosition = HitToGeoPosition(hitInfo);
        Debug.Log("Hit " + hitInfo.point.x + ", " + hitInfo.point.y + ", " + hitInfo.point.z);
        Debug.Log("GeoPosition " + geoPosition.X + ", " + geoPosition.Y + ", " + geoPosition.Z);

        var locationComponent = hitGobj.GetComponent<ArcGISLocationComponent>();
        locationComponent.enabled = true;
        locationComponent.Position = geoPosition;
        locationComponent.Rotation = new ArcGISRotation(0, 90, 0);
    }

    /// <summary>
    /// Return GeoPosition Based on RaycastHit; I.E. Where the user clicked in the Scene.
    /// </summary>
    /// <param name="hit"></param>
    /// <returns></returns>
    private ArcGISPoint HitToGeoPosition(RaycastHit hit, float yOffset = 0)
    {
        var rup = hpRoot.RootUniversePosition;

        var v3 = new double3(
            hit.point.x + rup.x,
            hit.point.y + rup.y + yOffset,
            hit.point.z + rup.z
            );

        // Spatial Reference of geoPosition will be Determined Spatial Reference of layers currently being rendered
        var geoPosition = arcGISMapComponent.View.WorldToGeographic(v3);

        return GeoUtils.ProjectToSpatialReference(geoPosition, new ArcGISSpatialReference(4326));
    }

    // The ArcGIS Rebase component
    private void RebaseCar(GameObject car)
    {
        var rootPosition = arcGISMapComponent.GetComponent<HPRoot>().RootUniversePosition;
        var delta = (lastRootPosition - rootPosition).ToVector3();
        if (delta.magnitude > 1) // 1km
        {
            car.transform.position += delta;
            lastRootPosition = rootPosition;
        }
    }
}
