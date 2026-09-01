import type { ComponentPropsWithoutRef } from "react";

type DocumentLinkProps = ComponentPropsWithoutRef<"a"> & {
  href: string;
};

export default function DocumentLink({ children, ...props }: DocumentLinkProps) {
  return <a {...props}>{children}</a>;
}
