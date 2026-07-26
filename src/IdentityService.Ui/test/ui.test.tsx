import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { ConfirmDialog, ScopeList } from "@/components/ui";

describe("identity UI controls", () => {
  it("keeps required scopes selected and disabled", () => {
    render(
      <ScopeList
        title="Identity"
        scopes={[
          {
            name: "openid",
            value: "openid",
            displayName: "User identifier",
            description: "Required by OpenID Connect",
            emphasize: false,
            required: true,
            checked: true,
            resources: [],
          },
        ]}
        selected={new Set()}
        onChange={vi.fn()}
      />,
    );

    const scope = screen.getByRole("checkbox", { name: /user identifier/i });
    expect(scope).toBeChecked();
    expect(scope).toBeDisabled();
  });

  it("requires confirmation before a destructive action", () => {
    const confirm = vi.fn();
    const cancel = vi.fn();
    render(
      <ConfirmDialog
        open
        title="Remove session?"
        description="The device will need to sign in again."
        confirmLabel="REMOVE"
        onConfirm={confirm}
        onCancel={cancel}
      />,
    );

    expect(screen.getByRole("button", { name: "CANCEL" })).toHaveFocus();
    fireEvent.click(screen.getByRole("button", { name: "REMOVE" }));
    expect(confirm).toHaveBeenCalledOnce();
    expect(cancel).not.toHaveBeenCalled();
  });

});
