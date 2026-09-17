import { useEffect, useMemo, useRef } from "react";
import { Link } from "react-router-dom";
import L from "leaflet";
import "leaflet/dist/leaflet.css";
import { CircleMarker, MapContainer, Marker, Popup, TileLayer, useMap, useMapEvents } from "react-leaflet";
import { env } from "@/config/env";
import type { Bounds, GymPin } from "@/features/gyms/api";
import { buildPinHtml } from "@/features/map/pinHtml";

/** Brand pin as an HTML marker (no image assets, so no bundler icon-path issues). */
function pinIcon(name: string, logoUrl: string | null) {
  return L.divIcon({ className: "gym-pin", html: buildPinHtml(name, logoUrl), iconSize: [40, 50], iconAnchor: [20, 50], popupAnchor: [0, -46] });
}

export const ITALY = { lat: 42.5, lng: 12.5, zoom: 6 };

function BoundsWatcher({ onBounds }: { onBounds: (b: Bounds) => void }) {
  const map = useMapEvents({
    moveend: () => emit(),
  });
  const timer = useRef<number>();
  const emit = () => {
    window.clearTimeout(timer.current);
    timer.current = window.setTimeout(() => {
      const b = map.getBounds();
      onBounds({ south: b.getSouth(), west: b.getWest(), north: b.getNorth(), east: b.getEast() });
    }, 250);
  };
  useEffect(() => { emit(); return () => window.clearTimeout(timer.current); }, []); // eslint-disable-line react-hooks/exhaustive-deps
  return null;
}

function Recenter({ center, zoom }: { center: { lat: number; lng: number } | null; zoom: number }) {
  const map = useMap();
  const key = center ? `${center.lat.toFixed(4)},${center.lng.toFixed(4)}` : "";
  useEffect(() => { if (center) map.setView([center.lat, center.lng], zoom, { animate: true }); }, [key]); // eslint-disable-line react-hooks/exhaustive-deps
  return null;
}

interface GymMapProps {
  pins: GymPin[];
  userPosition: { lat: number; lng: number } | null;
  /** When it changes, the map flies there. */
  focus: { lat: number; lng: number; zoom: number } | null;
  onBounds: (b: Bounds) => void;
}

/** Explore map: user position (blue dot) and gym pins with a popup linking to the gym. */
export function GymMap({ pins, userPosition, focus, onBounds }: GymMapProps) {
  const start = useMemo(() => focus ?? (userPosition ? { ...userPosition, zoom: 12 } : ITALY), []); // eslint-disable-line react-hooks/exhaustive-deps
  return (
    <MapContainer center={[start.lat, start.lng]} zoom={start.zoom} className="gym-map" scrollWheelZoom attributionControl>
      <TileLayer url={env.mapTileUrl} attribution={env.mapAttribution} maxZoom={19} />
      <BoundsWatcher onBounds={onBounds} />
      <Recenter center={focus} zoom={focus?.zoom ?? 12} />
      {userPosition && (
        <CircleMarker center={[userPosition.lat, userPosition.lng]} radius={8} pathOptions={{ color: "#fff", weight: 3, fillColor: "#2F6FDB", fillOpacity: 1 }}>
          <Popup>You are here</Popup>
        </CircleMarker>
      )}
      {pins.map((p) => (
        <Marker key={p.id} position={[p.latitude, p.longitude]} icon={pinIcon(p.name, p.logoUrl)} title={p.name} alt={p.name}>
          <Popup>
            <div className="pin-popup">
              <strong>{p.name}</strong>
              <span>{p.city}</span>
              <Link to={`/gyms/${p.slug}`}>Open gym</Link>
            </div>
          </Popup>
        </Marker>
      ))}
    </MapContainer>
  );
}

/** Small map for picking a gym's position: tap to place, drag to adjust. */
export function LocationPicker({ value, onChange }: { value: { lat: number; lng: number } | null; onChange: (v: { lat: number; lng: number }) => void }) {
  const start = value ?? ITALY;
  return (
    <MapContainer center={[start.lat, start.lng]} zoom={value ? 16 : ITALY.zoom} className="gym-map gym-map--picker" scrollWheelZoom={false}>
      <TileLayer url={env.mapTileUrl} attribution={env.mapAttribution} maxZoom={19} />
      <PickerEvents onPick={onChange} />
      <Recenter center={value} zoom={16} />
      {value && (
        <Marker position={[value.lat, value.lng]} draggable icon={pinIcon("+", null)}
          eventHandlers={{ dragend: (e) => { const ll = (e.target as L.Marker).getLatLng(); onChange({ lat: ll.lat, lng: ll.lng }); } }} />
      )}
    </MapContainer>
  );
}

function PickerEvents({ onPick }: { onPick: (v: { lat: number; lng: number }) => void }) {
  useMapEvents({ click: (e) => onPick({ lat: e.latlng.lat, lng: e.latlng.lng }) });
  return null;
}

/** Read-only map with one pin (gym Info tab). */
export function StaticGymMap({ lat, lng, name }: { lat: number; lng: number; name: string }) {
  return (
    <MapContainer center={[lat, lng]} zoom={15} className="gym-map gym-map--static" scrollWheelZoom={false} dragging={false} zoomControl={false} doubleClickZoom={false} touchZoom={false}>
      <TileLayer url={env.mapTileUrl} attribution={env.mapAttribution} />
      <Marker position={[lat, lng]} icon={pinIcon(name, null)} title={name} />
    </MapContainer>
  );
}
