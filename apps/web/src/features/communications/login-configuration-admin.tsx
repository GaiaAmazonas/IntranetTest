"use client";

import Link from "@/components/document-link";
import { useFeedback } from "@/components/feedback";
import { ConfirmDialog } from "@/components/form-dialog";
import { useSecurity } from "@/components/security-context";
import { apiRequest } from "@/lib/api-client";
import { AlertCircle, CheckCircle2, ImageIcon, LoaderCircle, Pencil, Plus, Rocket, Trash2, X } from "lucide-react";
import Image from "next/image";
import { FormEvent, useCallback, useEffect, useState } from "react";
import "./communications.css";

const apiUrl = process.env.NEXT_PUBLIC_GAIA_API_URL ?? "https://localhost:7168";
type Variant = "desktop" | "tablet" | "mobile";
type Social = { id: string; name: string; label: string; order: number; url: string };
type Configuration = {
  id: string; name: string; code: string; eyebrow: string; imageAlt?: string | null;
  description?: string | null; status: number; isCurrent: boolean; publishedAt?: string | null;
  hasDesktopImage: boolean; hasTabletImage: boolean; hasMobileImage: boolean;
  platformName?: string | null; lowerLeftText?: string | null; footerTitle?: string | null;
  footerDescription?: string | null; socialNetworks: Social[];
};
type Editor = {
  item?: Configuration; desktop?: File; name: string; code: string; eyebrow: string;
  imageAlt: string; description: string; platformName: string; lowerLeftText: string;
  footerTitle: string; footerDescription: string;
};
type SocialEditor = { configuration: Configuration; item?: Social; name: string; label: string; order: number; url: string };
type ImageOperation = "uploading" | "uploaded" | "deleting";
const emptyEditor: Editor = { name: "", code: "", eyebrow: "", imageAlt: "", description: "", platformName: "", lowerLeftText: "", footerTitle: "", footerDescription: "" };

export function LoginConfigurationAdmin() {
  const security = useSecurity();
  const { notify } = useFeedback();
  const [items, setItems] = useState<Configuration[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [editor, setEditor] = useState<Editor | null>(null);
  const [socialEditor, setSocialEditor] = useState<SocialEditor | null>(null);
  const [saving, setSaving] = useState(false);
  const [imageRevision, setImageRevision] = useState(0);
  const [imageOperations, setImageOperations] = useState<Record<string, ImageOperation>>({});
  const [actionBusy, setActionBusy] = useState("");
  const [removeImageTarget, setRemoveImageTarget] = useState<{ item: Configuration; variant: Variant } | null>(null);
  const [deleteSocialTarget, setDeleteSocialTarget] = useState<{ configuration: Configuration; item: Social } | null>(null);
  const [retireTarget, setRetireTarget] = useState<Configuration | null>(null);

  const load = useCallback(async () => {
    setLoading(true); setError("");
    try { setItems(await apiRequest<Configuration[]>("/api/communications/login-configurations")); }
    catch (reason) { setError(reason instanceof Error ? reason.message : "No fue posible cargar las configuraciones."); }
    finally { setLoading(false); }
  }, []);
  useEffect(() => { const timer = window.setTimeout(() => void load(), 0); return () => window.clearTimeout(timer); }, [load]);

  const edit = (item?: Configuration) => setEditor(item ? {
    item, name: item.name, code: item.code, eyebrow: item.eyebrow, imageAlt: item.imageAlt ?? "",
    description: item.description ?? "", platformName: item.platformName ?? "",
    lowerLeftText: item.lowerLeftText ?? "", footerTitle: item.footerTitle ?? "",
    footerDescription: item.footerDescription ?? "",
  } : { ...emptyEditor });
  const payload = (value: Editor) => ({
    name: value.name, code: value.code, eyebrow: value.eyebrow, imageAlt: value.imageAlt || null,
    description: value.description || null, platformName: value.platformName || null,
    lowerLeftText: value.lowerLeftText || null, footerTitle: value.footerTitle || null,
    footerDescription: value.footerDescription || null,
  });

  async function save(event: FormEvent) {
    event.preventDefault(); if (!editor) return;
    if (!editor.item && !editor.desktop) { setError("La imagen de escritorio es obligatoria al crear la configuración."); return; }
    setSaving(true); setError("");
    try {
      if (editor.item) await apiRequest(`/api/communications/login-configurations/${editor.item.id}`, { method: "PUT", body: JSON.stringify(payload(editor)) });
      else {
        const form = new FormData(); form.set("configuration", JSON.stringify(payload(editor))); form.set("desktopImage", editor.desktop!);
        await apiRequest("/api/communications/login-configurations", { method: "POST", body: form });
      }
      setEditor(null); await load();
      notify({ tone: "success", title: editor.item ? "Configuración actualizada" : "Configuración creada", description: "Los cambios se guardaron correctamente." });
    } catch (reason) { notify({ tone: "error", title: "No fue posible guardar", description: reason instanceof Error ? reason.message : "No fue posible guardar." }); }
    finally { setSaving(false); }
  }
  async function action(item: Configuration, name: "publish" | "retire") {
    if (actionBusy) return; setError(""); setActionBusy(`${item.id}:${name}`);
    try { await apiRequest(`/api/communications/login-configurations/${item.id}/${name}`, { method: "POST" }); await load(); if(name==="retire")setRetireTarget(null); notify({ tone: "success", title: name === "publish" ? "Login publicado" : "Publicación retirada", description: name === "publish" ? "La configuración quedó disponible como publicación vigente." : "La configuración dejó de estar publicada; sus datos e imágenes se conservaron." }); }
    catch (reason) { notify({ tone: "error", title: "No fue posible completar la operación", description: reason instanceof Error ? reason.message : "No fue posible cambiar el estado." }); }
    finally { setActionBusy(""); }
  }
  async function upload(item: Configuration, variant: Variant, file?: File) {
    if (!file) return; const form = new FormData(); form.set("file", file); setError("");
    const key = `${item.id}:${variant}`;
    setImageOperations(value => ({ ...value, [key]: "uploading" }));
    try {
      await apiRequest(`/api/communications/login-configurations/${item.id}/images/${variant}`, { method: "PUT", body: form });
      setImageRevision(value => value + 1); await load();
      setImageOperations(value => ({ ...value, [key]: "uploaded" }));
      notify({ tone: "success", title: "Imagen cargada", description: `La imagen para ${variantLabel(variant).toLowerCase()} quedó disponible.` });
      window.setTimeout(() => setImageOperations(value => { const next = { ...value }; delete next[key]; return next; }), 2600);
    } catch (reason) {
      setImageOperations(value => { const next = { ...value }; delete next[key]; return next; });
      notify({ tone: "error", title: "No fue posible cargar la imagen", description: reason instanceof Error ? reason.message : "No fue posible cargar la imagen." });
    }
  }
  async function removeImage() {
    if (!removeImageTarget) return; const { item, variant } = removeImageTarget;
    setError(""); const key = `${item.id}:${variant}`;
    setImageOperations(value => ({ ...value, [key]: "deleting" }));
    try {
      await apiRequest(`/api/communications/login-configurations/${item.id}/images/${variant}`, { method: "DELETE" });
      setImageRevision(value => value + 1); await load(); setRemoveImageTarget(null);
      notify({ tone: "success", title: "Imagen eliminada", description: `La vista para ${variantLabel(variant).toLowerCase()} utilizará la imagen de respaldo.` });
    } catch (reason) { notify({ tone: "error", title: "No fue posible eliminar la imagen", description: reason instanceof Error ? reason.message : "No fue posible eliminar la imagen." }); }
    finally { setImageOperations(value => { const next = { ...value }; delete next[key]; return next; }); }
  }
  async function saveSocial(event: FormEvent) {
    event.preventDefault(); if (!socialEditor) return; setSaving(true); setError("");
    try {
      const path = `/api/communications/login-configurations/${socialEditor.configuration.id}/socials${socialEditor.item ? `/${socialEditor.item.id}` : ""}`;
      await apiRequest(path, { method: socialEditor.item ? "PUT" : "POST", body: JSON.stringify({ name: socialEditor.name, label: socialEditor.label, order: socialEditor.order, url: socialEditor.url }) });
      setSocialEditor(null); await load(); notify({ tone: "success", title: socialEditor.item ? "Red social actualizada" : "Red social agregada", description: "El cambio se guardó correctamente." });
    } catch (reason) { notify({ tone: "error", title: "No fue posible guardar la red social", description: reason instanceof Error ? reason.message : "No fue posible guardar la red social." }); }
    finally { setSaving(false); }
  }
  async function deleteSocial() {
    if (!deleteSocialTarget) return; const { configuration, item } = deleteSocialTarget; setSaving(true);
    try { await apiRequest(`/api/communications/login-configurations/${configuration.id}/socials/${item.id}`, { method: "DELETE" }); await load(); setDeleteSocialTarget(null); notify({ tone: "success", title: "Red social eliminada", description: `${item.label} dejó de mostrarse en el login.` }); }
    catch (reason) { notify({ tone: "error", title: "No fue posible eliminar la red social", description: reason instanceof Error ? reason.message : "No fue posible eliminar la red social." }); }
    finally { setSaving(false); }
  }

  return <div className="comms-page login-admin">
    <nav aria-label="Secciones de Configuración" className="comms-section-nav"><Link className="is-active" href="/configuracion/login">Login institucional</Link></nav>
    <header className="comms-hero login-admin-hero"><div><p>Configuración · Acceso público</p><h1>Login institucional</h1><span>Administra el contenido visible antes del inicio de sesión.</span></div>{security.can("COM.DESTACADOS.CREAR") && <div className="comms-hero-actions"><button onClick={() => edit()} type="button"><Plus size={18} />Nueva configuración</button></div>}</header>
    {error && <div aria-live="assertive" className="comms-form-error login-dismissible-alert" role="alert"><AlertCircle aria-hidden="true" size={19} /><div><strong>No fue posible completar la operación</strong><p>{error}</p></div><button aria-label="Cerrar mensaje de error" onClick={() => setError("")} title="Cerrar" type="button"><X aria-hidden="true" size={17} /></button></div>}
    {loading ? <div className="comms-empty"><LoaderCircle className="gaia-spin" />Cargando configuraciones…</div> : items.length === 0 ? <div className="comms-empty"><ImageIcon />Aún no hay configuraciones de Login.</div> :
      <section className="login-config-list">{items.map(item => <article className={item.isCurrent ? "is-current" : ""} key={item.id}>
        <header><div><small>{item.code} · {status(item.status)}</small><h2>{item.name}</h2><p>{item.eyebrow}</p></div><div className="login-card-actions">{item.isCurrent && <b>Vigente</b>}{security.can("COM.DESTACADOS.ACTUALIZAR") && <button disabled={Boolean(actionBusy)} onClick={() => edit(item)} type="button"><Pencil size={15} />Editar</button>}{security.can("COM.DESTACADOS.ADMINISTRAR") && <button className="is-primary" disabled={Boolean(actionBusy)} onClick={() => void action(item, "publish")} type="button">{actionBusy===`${item.id}:publish`?<LoaderCircle className="gaia-spin" size={15}/>:<Rocket size={15} />}{actionBusy===`${item.id}:publish`?"Publicando…":item.status === 2 ? "Actualizar publicación" : "Publicar"}</button>}{security.can("COM.DESTACADOS.ADMINISTRAR") && item.status === 2 && <button className="is-danger" disabled={Boolean(actionBusy)} onClick={() => setRetireTarget(item)} type="button">Retirar</button>}</div></header>
        <aside className="login-image-guidance"><ImageIcon size={18} /><div><strong>Imágenes preparadas para cada pantalla</strong><p>Usa WebP o JPG para menor peso; PNG también está permitido. Máximo 8 MB. Mantén el elemento principal cerca del centro para que el recorte adaptable no oculte información importante.</p></div></aside>
        <div className="login-image-status">{(["desktop", "tablet", "mobile"] as Variant[]).map(variant => <ImageSlot item={item} key={variant} operation={imageOperations[`${item.id}:${variant}`]} remove={(target,kind)=>setRemoveImageTarget({item:target,variant:kind})} revision={imageRevision} upload={upload} variant={variant} />)}</div>
        <section className="login-socials"><header><strong>Redes sociales</strong>{security.can("COM.DESTACADOS.ACTUALIZAR") && <button onClick={() => setSocialEditor({ configuration: item, name: "", label: "", order: item.socialNetworks.length + 1, url: "https://" })} type="button"><Plus size={14} />Agregar</button>}</header>{item.socialNetworks.length === 0 ? <p>Sin redes asociadas.</p> : item.socialNetworks.map(social => <div key={social.id}><span><b>{social.order}</b><strong>{social.label}</strong><small>{social.url}</small></span>{security.can("COM.DESTACADOS.ACTUALIZAR") && <nav><button aria-label="Editar" onClick={() => setSocialEditor({ configuration: item, item: social, name: social.name, label: social.label, order: social.order, url: social.url })} type="button"><Pencil size={14} /></button><button aria-label="Eliminar" onClick={() => setDeleteSocialTarget({configuration:item,item:social})} type="button"><Trash2 size={14} /></button></nav>}</div>)}</section>
      </article>)}</section>}
    {editor && <ConfigurationDialog editor={editor} saving={saving} setEditor={setEditor} submit={save} />}
    {socialEditor && <SocialDialog editor={socialEditor} saving={saving} setEditor={setSocialEditor} submit={saveSocial} />}
    <ConfirmDialog confirmLabel="Sí, eliminar imagen" description={removeImageTarget?`La imagen para ${variantLabel(removeImageTarget.variant).toLowerCase()} se eliminará. Esa pantalla utilizará automáticamente la imagen de respaldo disponible.`:""} destructive loading={Boolean(removeImageTarget&&imageOperations[`${removeImageTarget.item.id}:${removeImageTarget.variant}`])} onCancel={()=>setRemoveImageTarget(null)} onConfirm={()=>void removeImage()} open={Boolean(removeImageTarget)} title="¿Eliminar esta imagen?" />
    <ConfirmDialog confirmLabel="Sí, eliminar red" description={deleteSocialTarget?`${deleteSocialTarget.item.label} dejará de mostrarse en el login institucional. Las demás redes no se modificarán.`:""} destructive loading={saving} onCancel={()=>setDeleteSocialTarget(null)} onConfirm={()=>void deleteSocial()} open={Boolean(deleteSocialTarget)} title="¿Eliminar esta red social?" />
    <ConfirmDialog confirmLabel="Sí, retirar publicación" description={retireTarget?`“${retireTarget.name}” dejará de mostrarse como login vigente. La configuración, sus imágenes y redes sociales se conservarán para futuras ediciones.`:""} destructive loading={Boolean(actionBusy)} onCancel={()=>setRetireTarget(null)} onConfirm={()=>retireTarget&&void action(retireTarget,"retire")} open={Boolean(retireTarget)} title="¿Retirar esta publicación?" />
  </div>;
}

function ImageSlot({ item, operation, remove, revision, upload, variant }: { item: Configuration; operation?: ImageOperation; remove: (item: Configuration, variant: Variant) => void; revision: number; upload: (item: Configuration, variant: Variant, file?: File) => Promise<void>; variant: Variant }) {
  const available = variant === "desktop" ? item.hasDesktopImage : variant === "tablet" ? item.hasTabletImage : item.hasMobileImage;
  const fallback = variant === "tablet" ? "Usará escritorio" : item.hasTabletImage ? "Usará tablet" : "Usará escritorio";
  const busy = operation === "uploading" || operation === "deleting";
  return <article aria-busy={busy} className={`login-image-slot${operation ? ` is-${operation}` : ""}`}><div>{available ? <Image alt={`Vista previa ${variantLabel(variant)}`} height={180} sizes="(max-width: 700px) 100vw, 33vw" src={`${apiUrl}/api/communications/login-configurations/${item.id}/images/${variant}?v=${revision}`} width={480} /> : <span><ImageIcon size={21} />{variant === "desktop" ? "Sin imagen disponible" : fallback}</span>}{operation && <div aria-live="polite" className="login-image-progress">{operation === "uploaded" ? <CheckCircle2 size={22} /> : <LoaderCircle className="gaia-spin" size={22} />}<strong>{operation === "uploading" ? "Cargando imagen…" : operation === "deleting" ? "Eliminando imagen…" : "Imagen cargada"}</strong>{operation === "uploading" && <small>Espera un momento, no cierres esta página.</small>}</div>}</div><footer><span><strong>{variantLabel(variant)}</strong><small>{available ? `Imagen cargada · ${imageRecommendation(variant)}` : `${variant === "desktop" ? "Carga requerida" : fallback} · ${imageRecommendation(variant)}`}</small></span><nav>{available && variant !== "desktop" && <button aria-label={`Eliminar imagen ${variantLabel(variant)}`} className="login-image-delete" disabled={busy} onClick={() => void remove(item, variant)} type="button"><Trash2 size={13} />Eliminar</button>}<label aria-disabled={busy} className={`comms-upload${busy ? " is-disabled" : ""}`}><input accept="image/jpeg,image/png,image/webp" disabled={busy} onChange={event => { const file = event.target.files?.[0]; event.target.value = ""; void upload(item, variant, file); }} type="file" />{operation === "uploading" ? "Cargando…" : available ? "Cambiar" : "Cargar"}</label></nav></footer></article>;
}

function ConfigurationDialog({ editor, saving, setEditor, submit }: { editor: Editor; saving: boolean; setEditor: (value: Editor | null) => void; submit: (event: FormEvent) => Promise<void> }) {
  return <div className="comms-dialog-backdrop"><section className="comms-dialog"><header><div><small>LOGIN INSTITUCIONAL</small><h2>{editor.item ? "Editar configuración" : "Nueva configuración"}</h2></div><button onClick={() => setEditor(null)} type="button">×</button></header><form onSubmit={submit}><div className="comms-form">{field("Nombre", "name", editor, setEditor, true)}{field("Código", "code", editor, setEditor, true)}{field("Antetítulo principal", "eyebrow", editor, setEditor, true)}{field("Texto alternativo imagen", "imageAlt", editor, setEditor)}{field("Nombre plataforma", "platformName", editor, setEditor)}{field("Texto inferior izquierdo", "lowerLeftText", editor, setEditor)}{field("Título pie", "footerTitle", editor, setEditor)}{field("Descripción pie", "footerDescription", editor, setEditor)}<label className="is-wide">Descripción principal<textarea onChange={event => setEditor({ ...editor, description: event.target.value })} value={editor.description} /></label>{!editor.item && <label className="is-wide">Imagen escritorio *<small>Recomendado: 1920×1200 px (16:10), WebP o JPG, máximo 8 MB. Mantén el foco principal en el centro.</small><input accept="image/jpeg,image/png,image/webp" onChange={event => setEditor({ ...editor, desktop: event.target.files?.[0] })} required type="file" /></label>}</div><footer><button onClick={() => setEditor(null)} type="button">Cancelar</button><button disabled={saving} type="submit">{saving ? "Guardando…" : "Guardar"}</button></footer></form></section></div>;
}
function SocialDialog({ editor, saving, setEditor, submit }: { editor: SocialEditor; saving: boolean; setEditor: (value: SocialEditor | null) => void; submit: (event: FormEvent) => Promise<void> }) {
  return <div className="comms-dialog-backdrop"><section className="comms-dialog login-social-dialog"><header><div><small>RED SOCIAL</small><h2>{editor.item ? "Editar" : "Agregar"}</h2></div><button onClick={() => setEditor(null)} type="button">×</button></header><form onSubmit={submit}><div className="comms-form">{socialField("Nombre", "name", editor, setEditor)}{socialField("Etiqueta", "label", editor, setEditor)}<label>Orden<input min="0" onChange={event => setEditor({ ...editor, order: Number(event.target.value) })} required type="number" value={editor.order} /></label>{socialField("URL", "url", editor, setEditor)}</div><footer><button onClick={() => setEditor(null)} type="button">Cancelar</button><button disabled={saving} type="submit">Guardar</button></footer></form></section></div>;
}
function field(label: string, key: keyof Editor, value: Editor, set: (value: Editor) => void, required = false) { return <label>{label}{required ? " *" : ""}<input onChange={event => set({ ...value, [key]: event.target.value })} required={required} value={String(value[key] ?? "")} /></label>; }
function socialField(label: string, key: "name" | "label" | "url", value: SocialEditor, set: (value: SocialEditor) => void) { return <label>{label} *<input onChange={event => set({ ...value, [key]: event.target.value })} required type={key === "url" ? "url" : "text"} value={value[key]} /></label>; }
function status(value: number) { return value === 2 ? "Publicada" : value === 3 ? "Retirada" : "Borrador"; }
function variantLabel(value: Variant) { return value === "desktop" ? "Escritorio" : value === "tablet" ? "Tablet" : "Móvil"; }
function imageRecommendation(value: Variant) { return value === "desktop" ? "1920×1200 px (16:10)" : value === "tablet" ? "1600×1000 px (16:10)" : "1080×1350 px (4:5)"; }
