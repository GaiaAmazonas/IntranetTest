"use client";

import { apiRequest, apiUrlFor } from "@/lib/api-client";
import { Download, FileText, LoaderCircle } from "lucide-react";
import { useEffect, useState } from "react";

export function TrainingDocumentViewer({ resourcePath, title }: { resourcePath: string; title: string }) {
  const [result, setResult] = useState<{path:string;url:string;error:string}>({path:"",url:"",error:""});
  const current = result.path === resourcePath ? result : {path:resourcePath,url:"",error:""};

  useEffect(() => {
    const controller = new AbortController();
    void apiRequest<{url:string}>(`${resourcePath}/preview`, { cache: "no-store", signal: controller.signal })
      .then(preview => setResult({path:resourcePath,url:preview.url,error:""}))
      .catch(reason => {
        if (reason instanceof DOMException && reason.name === "AbortError") return;
        setResult({path:resourcePath,url:"",error:reason instanceof Error ? reason.message : "No fue posible preparar la vista previa del documento."});
      });
    return () => {
      controller.abort();
    };
  }, [resourcePath]);

  return <section className="mt-4 overflow-hidden rounded-2xl border border-[var(--gaia-line)] bg-white">
    <header className="flex flex-wrap items-center justify-between gap-3 border-b border-[var(--gaia-line)] px-4 py-3">
      <span className="flex min-w-0 items-center gap-2 text-sm font-semibold"><FileText className="shrink-0" size={18}/><span className="truncate">{title}</span></span>
      <a className="gaia-button gaia-button-secondary !min-h-9 !px-3 !text-xs" href={apiUrlFor(`${resourcePath}?download=true`)} rel="noopener noreferrer" target="_blank"><Download size={15}/>Descargar original</a>
    </header>
    {!current.url&&!current.error&&<div aria-busy="true" className="grid min-h-64 place-items-center p-6 text-sm text-[var(--gaia-ink-500)]"><span className="flex items-center gap-2"><LoaderCircle className="animate-spin" size={18}/>Preparando vista previa segura…</span></div>}
    {current.error&&<div className="grid min-h-44 place-items-center p-6 text-center"><div><p className="font-semibold">Vista previa no disponible</p><p className="mt-2 max-w-xl text-sm text-[var(--gaia-ink-500)]">{current.error}</p><p className="mt-1 text-xs text-[var(--gaia-ink-500)]">El archivo original continúa disponible mediante el botón de descarga.</p></div></div>}
    {current.url&&<iframe className="h-[70vh] min-h-[480px] w-full bg-[var(--gaia-surface-subtle)]" referrerPolicy="no-referrer" sandbox="allow-forms allow-popups allow-same-origin allow-scripts" src={current.url} title={`Vista previa de ${title}`}/>}
  </section>;
}
