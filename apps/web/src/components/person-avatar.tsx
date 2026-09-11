"use client";
/* eslint-disable @next/next/no-img-element -- The authenticated API image URL cannot be optimized by Next. */

import { useEffect, useRef, useState, type CSSProperties } from "react";

const apiUrl = process.env.NEXT_PUBLIC_GAIA_API_URL ?? "https://localhost:7168";
const supportedPhotoSizes = [48, 64, 96, 120, 240] as const;

export function personInitials(name: string) {
  return name.trim().split(/\s+/).filter(Boolean).slice(0, 2).map(part => part[0]).join("").toUpperCase() || "GA";
}

export function profilePhotoPath(personId?: string, currentUser = false, size = 96) {
  // The UI can display any size, but Graph only exposes these predefined variants.
  // Request the nearest available image and let CSS render it at the requested size.
  const photoSize = supportedPhotoSizes.reduce((closest, candidate) =>
    Math.abs(candidate - size) < Math.abs(closest - size) ? candidate : closest);
  const query = `?size=${encodeURIComponent(String(photoSize))}`;
  return currentUser ? `/api/profile/photo${query}` : personId ? `/api/intranet/people/${encodeURIComponent(personId)}/photo${query}` : null;
}

type PersonAvatarProps = { name: string; personId?: string; currentUser?: boolean; size?: number; className?: string; imageUrl?: string | null; alt?: string };

export function PersonAvatar({ name, personId, currentUser = false, size = 48, className = "", imageUrl, alt }: PersonAvatarProps) {
  const containerRef = useRef<HTMLSpanElement>(null);
  const [visible, setVisible] = useState(currentUser || Boolean(imageUrl) || !personId);
  const [failed, setFailed] = useState(false);
  const endpoint = profilePhotoPath(personId, currentUser, size);

  useEffect(() => {
    if (currentUser || imageUrl || !endpoint) return;
    const element = containerRef.current;
    if (!element || !("IntersectionObserver" in window)) { setVisible(true); return; }
    const observer = new IntersectionObserver(entries => {
      if (entries.some(entry => entry.isIntersecting)) { setVisible(true); observer.disconnect(); }
    }, { rootMargin: "180px" });
    observer.observe(element);
    return () => observer.disconnect();
  }, [currentUser, endpoint, imageUrl]);

  useEffect(() => {
    setFailed(false);
  }, [endpoint, imageUrl]);

  // Use the protected image endpoint directly. It avoids a cross-origin fetch/blob
  // conversion, while the browser still sends the authenticated session cookie.
  const photoUrl = imageUrl ?? (visible && endpoint ? `${apiUrl}${endpoint}` : null);

  return <span aria-busy={visible && !photoUrl && Boolean(endpoint) || undefined} className={`gaia-person-avatar ${className}`} ref={containerRef}
    style={{ "--person-avatar-size": `${size}px` } as CSSProperties}>
    {photoUrl && !failed
      ? <img alt={alt ?? `Fotografía de ${name}`} crossOrigin="use-credentials" onError={() => setFailed(true)} referrerPolicy="no-referrer" src={photoUrl} />
      : <span aria-label={alt ?? name}>{personInitials(name)}</span>}
  </span>;
}
