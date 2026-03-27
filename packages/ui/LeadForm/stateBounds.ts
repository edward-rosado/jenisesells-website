export interface BoundingBox {
  north: number;
  south: number;
  east: number;
  west: number;
}

/**
 * Bounding boxes for US states. Used to restrict autocomplete suggestions
 * to the agent's service area. Add entries as new agents onboard.
 */
export const STATE_BOUNDING_BOXES: Record<string, BoundingBox> = {
  NJ: { north: 41.3574, south: 38.9285, east: -73.8938, west: -75.5598 },
  NY: { north: 45.0153, south: 40.4774, east: -71.8563, west: -79.7625 },
  CT: { north: 42.0505, south: 40.9509, east: -71.7874, west: -73.7278 },
  PA: { north: 42.2698, south: 39.7199, east: -74.6895, west: -80.5199 },
  FL: { north: 31.0009, south: 24.3963, east: -79.9743, west: -87.6349 },
  TX: { north: 36.5007, south: 25.8371, east: -93.5083, west: -106.6456 },
  CA: { north: 42.0095, south: 32.5343, east: -114.1312, west: -124.4096 },
};

/** Returns the bounding box for a state code, or undefined if not mapped. */
export function getStateBounds(stateCode: string): BoundingBox | undefined {
  return STATE_BOUNDING_BOXES[stateCode];
}
