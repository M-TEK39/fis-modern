"use client";

import { useReducer, useTransition } from "react";

import { loadDemoVehicleReportAction } from "@/app/(fleet-operations)/vehicles/demo/actions";
import type { DemoVehicleRecord } from "@/lib/api/vehicles/api-demo-vehicles";

export type DemoVehicleReportState = {
  loaded: boolean;
  rows: DemoVehicleRecord[];
  page: number;
  total: number;
  totalPages: number;
  error: string | null;
};

type ReportAction =
  | { type: "request" }
  | { type: "error"; message: string; reset: boolean }
  | {
      type: "success";
      report: NonNullable<Awaited<ReturnType<typeof loadDemoVehicleReportAction>>["report"]>;
    };

const INITIAL_REPORT_STATE: DemoVehicleReportState = {
  loaded: false,
  rows: [],
  page: 1,
  total: 0,
  totalPages: 0,
  error: null,
};

function reportReducer(
  state: DemoVehicleReportState,
  action: ReportAction,
): DemoVehicleReportState {
  switch (action.type) {
    case "request":
      return { ...state, error: null };
    case "error":
      return {
        ...state,
        loaded: true,
        rows: action.reset ? [] : state.rows,
        total: action.reset ? 0 : state.total,
        totalPages: action.reset ? 0 : state.totalPages,
        error: action.message,
      };
    case "success":
      return {
        ...state,
        loaded: true,
        rows: action.report.items,
        page: action.report.page,
        total: action.report.total,
        totalPages: action.report.totalPages,
        error: null,
      };
  }
}

export function useDemoVehicleReport() {
  const [state, dispatch] = useReducer(reportReducer, INITIAL_REPORT_STATE);
  const [isPending, startTransition] = useTransition();

  const loadReport = (requestedPage: number) => {
    const resetOnError = !state.loaded;
    dispatch({ type: "request" });
    startTransition(async () => {
      const result = await loadDemoVehicleReportAction(requestedPage);
      if (result.status === "error") {
        dispatch({
          type: "error",
          message: result.message ?? "The demo vehicle report could not be loaded.",
          reset: resetOnError,
        });
        return;
      }
      if (!result.report) {
        dispatch({
          type: "error",
          message: "The demo vehicle report returned an incomplete page.",
          reset: resetOnError,
        });
        return;
      }
      dispatch({ type: "success", report: result.report });
    });
  };

  return { ...state, isPending, loadReport };
}
