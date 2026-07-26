import { fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { RegisterPage } from "./register-page";

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("RegisterPage", () => {
  it("renders password requirements and validates password confirmation", async () => {
    const fetchMock = vi.fn(async () =>
      new Response(
        JSON.stringify({
          returnUrl: null,
          passwordRequirements: {
            requiredLength: 6,
            requiredUniqueChars: 1,
            requireDigit: true,
            requireLowercase: true,
            requireUppercase: true,
            requireNonAlphanumeric: true,
          },
        }),
        { status: 200, headers: { "Content-Type": "application/json" } },
      ),
    );
    vi.stubGlobal("fetch", fetchMock);

    render(<RegisterPage />);

    expect(await screen.findByRole("heading", { name: "Create account" })).toBeInTheDocument();
    expect(screen.getByText("At least 6 characters")).toBeInTheDocument();
    fireEvent.change(screen.getByLabelText("Username"), { target: { value: "driver" } });
    fireEvent.change(screen.getByLabelText("Email"), { target: { value: "driver@revora.test" } });
    fireEvent.change(screen.getByLabelText("Password"), { target: { value: "Secure1!" } });
    fireEvent.change(screen.getByLabelText("Confirm password"), {
      target: { value: "Different1!" },
    });
    fireEvent.click(screen.getByRole("button", { name: "CREATE ACCOUNT" }));

    expect(await screen.findByText("Passwords do not match.")).toBeInTheDocument();
    expect(fetchMock).toHaveBeenCalledOnce();
  });
});
