import { PageHeader, Panel } from "@/components/ui";
import Link from "next/link";

export default function NotFound() {
  return (
    <div>
      <PageHeader
        eyebrow="Identity navigation"
        title="Page not found"
        description="The requested Identity page does not exist or is not available from this connection."
      />
      <Panel>
        <Link href="/">RETURN TO IDENTITY HOME</Link>
      </Panel>
    </div>
  );
}
